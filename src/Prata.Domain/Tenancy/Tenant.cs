using Prata.Domain.Common;

namespace Prata.Domain.Tenancy;

/// <summary>
/// Agregado raiz do estudio. RN-TEN-001..012 (subset E1).
/// </summary>
public sealed class Tenant : AggregateRoot
{
    // EF Core
    private Tenant()
    {
        Slug = null!;
        Name = null!;
        Settings = null!;
    }

    private Tenant(Guid id, TenantSlug slug, string name, TenantSettings settings, TenantStatus status, DateTimeOffset createdAt)
        : base(id)
    {
        Slug = slug;
        Name = name;
        Settings = settings;
        Status = status;
        CreatedAt = createdAt;
        PortfolioPublishedAt = null;
    }

    public TenantSlug Slug { get; private set; }

    public string Name { get; private set; }

    public TenantSettings Settings { get; private set; }

    public TenantStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? PortfolioPublishedAt { get; private set; }

    public bool IsPortfolioPublished => PortfolioPublishedAt is not null;

    /// <summary>Dominio proprio do tenant (CNAME). Null = so subdominio (ADR-0004 / RN-TEN-013).</summary>
    public string? CustomDomain { get; private set; }

    public CustomDomainStatus CustomDomainStatus { get; private set; } = CustomDomainStatus.Nenhum;

    public DateTimeOffset? CustomDomainVerifiedAt { get; private set; }

    /// <summary>Opt-in Cloud API WhatsApp (RN-NOT-012). Default false = wa.me.</summary>
    public bool WhatsAppCloudEnabled { get; private set; }

    public static Result<Tenant> Create(string name, string slug, DateTimeOffset agora)
    {
        if (string.IsNullOrWhiteSpace(name))
            return TenancyErrors.NomeObrigatorio;

        var slugResult = TenantSlug.Create(slug);
        if (slugResult.IsFailure)
            return Result.Failure<Tenant>(slugResult.Error!.Value);

        var tenant = new Tenant(
            Guid.NewGuid(),
            slugResult.Value,
            name.Trim(),
            TenantSettings.CreateDefault(),
            TenantStatus.Rascunho,
            agora
        );

        tenant.Raise(new TenantCriado(tenant.Id, tenant.Slug.Value, tenant.Name));
        return tenant;
    }

    /// <summary>
    /// Altera o slug. Bloqueado apos a primeira publicacao do portfolio (RN-TEN-003).
    /// </summary>
    public Result<Unit> AlterarSlug(string novoSlug)
    {
        if (IsPortfolioPublished)
            return TenancyErrors.SlugImutavelAposPublicacao;

        var slugResult = TenantSlug.Create(novoSlug);
        if (slugResult.IsFailure)
            return Result.Failure<Unit>(slugResult.Error!.Value);

        Slug = slugResult.Value;
        return Unit.Value;
    }

    public Result<Unit> PublicarPortfolio(DateTimeOffset agora)
    {
        if (Status == TenantStatus.Suspenso)
            return TenancyErrors.TenantSuspenso;

        if (IsPortfolioPublished)
            return TenancyErrors.TenantJaPublicado;

        PortfolioPublishedAt = agora;
        Status = TenantStatus.Ativo;
        Raise(new PortfolioPublicado(Id, Slug.Value));
        return Unit.Value;
    }

    public Result<Unit> Suspender(string reason)
    {
        if (Status == TenantStatus.Suspenso)
            return TenancyErrors.TenantJaSuspenso;

        Status = TenantStatus.Suspenso;
        Raise(new TenantSuspenso(Id, reason));
        return Unit.Value;
    }

    public Result<Unit> Reativar()
    {
        if (Status != TenantStatus.Suspenso)
            return TenancyErrors.TenantNaoSuspenso;

        Status = IsPortfolioPublished ? TenantStatus.Ativo : TenantStatus.Rascunho;
        Raise(new TenantReativado(Id));
        return Unit.Value;
    }

    public Result<Unit> AlterarTema(ThemeId themeId)
    {
        Settings = Settings.WithTheme(themeId);
        Raise(new TemaAlterado(Id, themeId));
        return Unit.Value;
    }

    /// <summary>RN-TEN-013 — solicita dominio proprio; verificacao CNAME e SSL ficam na aplicacao.</summary>
    public Result<Unit> SolicitarDominioProprio(string domain)
    {
        if (string.IsNullOrWhiteSpace(domain))
            return Error.Validation("DOMINIO_OBRIGATORIO", "Dominio proprio e obrigatorio.");

        var normalized = domain.Trim().ToLowerInvariant();
        if (normalized.EndsWith(".prata.app", StringComparison.Ordinal))
            return Error.Validation("DOMINIO_RESERVADO", "Nao use subdominio da plataforma como dominio proprio.");
        if (normalized.Contains('/', StringComparison.Ordinal) || normalized.Contains(' ', StringComparison.Ordinal))
            return Error.Validation("DOMINIO_INVALIDO", "Informe apenas o hostname (ex.: studio.com.br).");

        CustomDomain = normalized;
        CustomDomainStatus = CustomDomainStatus.PendenteVerificacao;
        CustomDomainVerifiedAt = null;
        return Unit.Value;
    }

    public Result<Unit> MarcarDominioVerificado(DateTimeOffset agora)
    {
        if (string.IsNullOrWhiteSpace(CustomDomain) || CustomDomainStatus == CustomDomainStatus.Nenhum)
            return Error.Validation("DOMINIO_NAO_SOLICITADO", "Nenhum dominio proprio solicitado.");

        CustomDomainStatus = CustomDomainStatus.Ativo;
        CustomDomainVerifiedAt = agora;
        return Unit.Value;
    }

    public Result<Unit> RemoverDominioProprio()
    {
        CustomDomain = null;
        CustomDomainStatus = CustomDomainStatus.Nenhum;
        CustomDomainVerifiedAt = null;
        return Unit.Value;
    }

    public Result<Unit> DefinirWhatsAppCloud(bool enabled)
    {
        WhatsAppCloudEnabled = enabled;
        return Unit.Value;
    }
}
