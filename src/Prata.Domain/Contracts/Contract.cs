using Prata.Domain.Common;

namespace Prata.Domain.Contracts;

/// <summary>
/// Contrato do pedido. Assinado e imutavel (RN-CTR-011); PDF armazenado, nao regerado (ADR-0006).
/// </summary>
public sealed class Contract : AggregateRoot, ITenantOwned
{
    private readonly List<string> _clauseCodes = [];

    private Contract()
    {
        TemplateVersion = null!;
    }

    private Contract(
        Guid id,
        Guid tenantId,
        Guid orderId,
        string templateVersion,
        IEnumerable<string> clauseCodes,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt,
        Guid? replacesContractId
    )
        : base(id)
    {
        TenantId = tenantId;
        OrderId = orderId;
        TemplateVersion = templateVersion;
        _clauseCodes.AddRange(clauseCodes);
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
        ReplacesContractId = replacesContractId;
        Status = ContractStatus.Rascunho;
    }

    public Guid TenantId { get; }

    public Guid OrderId { get; }

    public ContractStatus Status { get; private set; }

    public string TemplateVersion { get; private set; }

    public string? PdfStorageKey { get; private set; }

    public string? PdfSha256 { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? SentAt { get; private set; }

    public DateTimeOffset? ViewedAt { get; private set; }

    public DateTimeOffset? SignedAt { get; private set; }

    public Guid? ReplacesContractId { get; }

    public Signature? Signature { get; private set; }

    public IReadOnlyList<string> ClauseCodes => _clauseCodes;

    public static Result<Contract> Create(
        Guid tenantId,
        Guid orderId,
        string templateVersion,
        IReadOnlyCollection<string> clauseCodes,
        DateTimeOffset agora,
        int validadeDias,
        Guid? replacesContractId = null
    )
    {
        if (tenantId == Guid.Empty || orderId == Guid.Empty)
            return Error.Validation("CONTRATO_IDS", "Tenant e pedido obrigatorios.");
        if (string.IsNullOrWhiteSpace(templateVersion))
            return Error.Validation("CONTRATO_TEMPLATE", "Versao de template obrigatoria.");
        if (validadeDias < 1)
            return Error.Validation("CONTRATO_VALIDADE", "Validade deve ser de pelo menos 1 dia.");

        var codes = clauseCodes.Select(c => c.Trim()).Where(c => c.Length > 0).Distinct(StringComparer.Ordinal).ToArray();
        foreach (var required in ContractRequiredClauses.All)
        {
            if (!codes.Contains(required, StringComparer.Ordinal))
                return ContractErrors.ClausulasObrigatorias;
        }

        return new Contract(
            Guid.NewGuid(),
            tenantId,
            orderId,
            templateVersion.Trim(),
            codes,
            agora,
            agora.AddDays(validadeDias),
            replacesContractId
        );
    }

    public Result<Unit> Enviar(string pdfStorageKey, string pdfSha256, DateTimeOffset agora)
    {
        if (Status == ContractStatus.Assinado)
            return ContractErrors.AssinadoImutavel;
        if (Status is not (ContractStatus.Rascunho or ContractStatus.Enviado))
            return ContractErrors.TransicaoInvalida(Status, nameof(ContractStatus.Enviado));
        if (string.IsNullOrWhiteSpace(pdfStorageKey))
            return Error.Validation("CONTRATO_PDF_KEY", "Chave de armazenamento do PDF obrigatoria.");
        if (string.IsNullOrWhiteSpace(pdfSha256) || pdfSha256.Trim().Length != 64)
            return ContractErrors.HashObrigatorio;

        PdfStorageKey = pdfStorageKey.Trim();
        PdfSha256 = pdfSha256.Trim().ToLowerInvariant();
        SentAt = agora;
        Status = ContractStatus.Enviado;
        Raise(new ContratoEnviado(TenantId, Id, OrderId));
        return Unit.Value;
    }

    public Result<Unit> RegistrarVisualizacao(string ip, string userAgent, DateTimeOffset agora)
    {
        if (Status == ContractStatus.Assinado)
            return ContractErrors.AssinadoImutavel;
        if (Status is not (ContractStatus.Enviado or ContractStatus.Visualizado))
            return ContractErrors.TransicaoInvalida(Status, nameof(ContractStatus.Visualizado));
        if (string.IsNullOrWhiteSpace(ip) || string.IsNullOrWhiteSpace(userAgent))
            return ContractErrors.AssinaturaIncompleta;

        ViewedAt ??= agora;
        Status = ContractStatus.Visualizado;
        Raise(new ContratoVisualizado(TenantId, Id, OrderId, ip.Trim(), userAgent.Trim()));
        return Unit.Value;
    }

    public Result<Unit> Assinar(
        string signerName,
        string signerEmail,
        string pdfSha256,
        string ip,
        string userAgent,
        DateTimeOffset agora
    )
    {
        if (Status == ContractStatus.Assinado)
            return ContractErrors.AssinadoImutavel;
        if (Status == ContractStatus.Expirado || agora > ExpiresAt)
            return ContractErrors.Expirado;
        if (Status is not (ContractStatus.Enviado or ContractStatus.Visualizado))
            return ContractErrors.TransicaoInvalida(Status, nameof(ContractStatus.Assinado));
        if (
            string.IsNullOrWhiteSpace(pdfSha256)
            || string.IsNullOrWhiteSpace(ip)
            || string.IsNullOrWhiteSpace(userAgent)
        )
            return ContractErrors.AssinaturaIncompleta;
        if (!string.Equals(PdfSha256, pdfSha256.Trim(), StringComparison.OrdinalIgnoreCase))
            return ContractErrors.HashDivergente;

        var signature = Signature.Create(TenantId, Id, signerName, signerEmail, pdfSha256, ip, userAgent, agora);
        if (signature.IsFailure)
            return Result.Failure<Unit>(signature.Error!.Value);

        Signature = signature.Value;
        SignedAt = agora;
        Status = ContractStatus.Assinado;
        Raise(new ContratoAssinado(TenantId, Id, OrderId, Signature.Id));
        return Unit.Value;
    }

    public Result<Unit> Recusar(string? motivo, DateTimeOffset agora)
    {
        _ = agora;
        if (Status is not (ContractStatus.Enviado or ContractStatus.Visualizado))
            return ContractErrors.TransicaoInvalida(Status, nameof(ContractStatus.Recusado));

        Status = ContractStatus.Recusado;
        Raise(new ContratoRecusado(TenantId, Id, OrderId, motivo?.Trim()));
        return Unit.Value;
    }

    public Result<Unit> Expirar(DateTimeOffset agora)
    {
        if (Status is ContractStatus.Assinado or ContractStatus.Cancelado or ContractStatus.Recusado or ContractStatus.Expirado)
            return ContractErrors.TransicaoInvalida(Status, nameof(ContractStatus.Expirado));
        if (agora <= ExpiresAt)
            return Error.Validation("CONTRATO_AINDA_VIGENTE", "Contrato ainda dentro da validade.");

        Status = ContractStatus.Expirado;
        Raise(new ContratoExpirado(TenantId, Id, OrderId));
        return Unit.Value;
    }

    public Result<Unit> Cancelar()
    {
        if (Status is ContractStatus.Assinado or ContractStatus.Expirado or ContractStatus.Recusado)
            return ContractErrors.TransicaoInvalida(Status, nameof(ContractStatus.Cancelado));
        if (Status is not (ContractStatus.Rascunho or ContractStatus.Enviado or ContractStatus.Visualizado))
            return ContractErrors.TransicaoInvalida(Status, nameof(ContractStatus.Cancelado));

        Status = ContractStatus.Cancelado;
        return Unit.Value;
    }

    /// <summary>RN-CTR-011 — correcao gera contrato novo com referencia ao assinado.</summary>
    public Result<Contract> CriarCorrecao(
        string templateVersion,
        IReadOnlyCollection<string> clauseCodes,
        DateTimeOffset agora,
        int validadeDias
    )
    {
        if (Status != ContractStatus.Assinado)
            return Error.Validation("CONTRATO_CORRECAO_SO_ASSINADO", "Correcao so a partir de contrato Assinado.");

        return Create(TenantId, OrderId, templateVersion, clauseCodes, agora, validadeDias, replacesContractId: Id);
    }
}

public sealed class Signature : Entity, ITenantOwned
{
    private Signature()
    {
        SignerName = null!;
        SignerEmail = null!;
        PdfSha256 = null!;
        Ip = null!;
        UserAgent = null!;
    }

    private Signature(
        Guid id,
        Guid tenantId,
        Guid contractId,
        string signerName,
        string signerEmail,
        string pdfSha256,
        string ip,
        string userAgent,
        DateTimeOffset signedAt
    )
        : base(id)
    {
        TenantId = tenantId;
        ContractId = contractId;
        SignerName = signerName;
        SignerEmail = signerEmail;
        PdfSha256 = pdfSha256;
        Ip = ip;
        UserAgent = userAgent;
        SignedAt = signedAt;
    }

    public Guid TenantId { get; }

    public Guid ContractId { get; }

    public string SignerName { get; }

    public string SignerEmail { get; }

    public string PdfSha256 { get; }

    public string Ip { get; }

    public string UserAgent { get; }

    public DateTimeOffset SignedAt { get; }

    public static Result<Signature> Create(
        Guid tenantId,
        Guid contractId,
        string signerName,
        string signerEmail,
        string pdfSha256,
        string ip,
        string userAgent,
        DateTimeOffset signedAt
    )
    {
        if (tenantId == Guid.Empty || contractId == Guid.Empty)
            return Error.Validation("ASSINATURA_CONTRATO", "Tenant e contrato obrigatorios.");
        if (string.IsNullOrWhiteSpace(signerName) || string.IsNullOrWhiteSpace(signerEmail))
            return Error.Validation("ASSINATURA_SIGNATARIO", "Nome e e-mail do signatario obrigatorios.");
        if (
            string.IsNullOrWhiteSpace(pdfSha256)
            || string.IsNullOrWhiteSpace(ip)
            || string.IsNullOrWhiteSpace(userAgent)
        )
            return ContractErrors.AssinaturaIncompleta;

        return new Signature(
            Guid.NewGuid(),
            tenantId,
            contractId,
            signerName.Trim(),
            signerEmail.Trim().ToLowerInvariant(),
            pdfSha256.Trim().ToLowerInvariant(),
            ip.Trim(),
            userAgent.Trim(),
            signedAt
        );
    }
}

public sealed record ContratoEnviado(Guid TenantId, Guid ContractId, Guid OrderId) : DomainEvent;

public sealed record ContratoVisualizado(Guid TenantId, Guid ContractId, Guid OrderId, string Ip, string UserAgent)
    : DomainEvent;

public sealed record ContratoAssinado(Guid TenantId, Guid ContractId, Guid OrderId, Guid SignatureId) : DomainEvent;

public sealed record ContratoRecusado(Guid TenantId, Guid ContractId, Guid OrderId, string? Motivo) : DomainEvent;

public sealed record ContratoExpirado(Guid TenantId, Guid ContractId, Guid OrderId) : DomainEvent;
