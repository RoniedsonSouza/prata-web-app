using Prata.Domain.Common;

namespace Prata.Domain.Billing;

/// <summary>Parcela com maquina por meio de pagamento (docs/05 §3).</summary>
public sealed class Installment : Entity, ITenantOwned
{
    private Installment()
    {
        Amount = Money.Zero();
    }

    private Installment(
        Guid id,
        Guid tenantId,
        Guid paymentId,
        int sequence,
        Money amount,
        DateOnly dueDate,
        PaymentMethod method
    )
        : base(id)
    {
        TenantId = tenantId;
        PaymentId = paymentId;
        Sequence = sequence;
        Amount = amount;
        DueDate = dueDate;
        Method = method;
        Status = InstallmentStatus.Pendente;
    }

    public Guid TenantId { get; }

    public Guid PaymentId { get; }

    public int Sequence { get; }

    public Money Amount { get; }

    public DateOnly DueDate { get; }

    public PaymentMethod Method { get; }

    public InstallmentStatus Status { get; private set; }

    public DateTimeOffset? ConfirmedAt { get; private set; }

    public DateTimeOffset? SettledAt { get; private set; }

    public string? ExternalInstallmentId { get; private set; }

    public static Result<Installment> Create(
        Guid tenantId,
        Guid paymentId,
        int sequence,
        Money amount,
        DateOnly dueDate,
        PaymentMethod method
    )
    {
        if (amount.Amount <= 0)
            return Error.Validation("PARCELA_VALOR_INVALIDO", "Parcela deve ser positiva.");

        return new Installment(Guid.NewGuid(), tenantId, paymentId, sequence, amount, dueDate, method);
    }

    public Result<Unit> Executar(string transicao, PaymentMethod method)
    {
        if (method != Method)
            return BillingErrors.MetodoNaoSuporta;

        return transicao switch
        {
            "MarcarProcessando" => MarcarProcessando(method),
            "Confirmar" => Confirmar(method, DateTimeOffset.UtcNow),
            "Expirar" => Expirar(method),
            "Liquidar" => Liquidar(method, DateTimeOffset.UtcNow),
            "Repassar" => Repassar(method),
            "SolicitarEstorno" => SolicitarEstorno(method),
            "ConfirmarEstorno" => ConfirmarEstorno(method, parcial: false),
            "Autorizar" => Autorizar(method),
            "Recusar" => Recusar(method),
            "AbrirChargeback" => AbrirChargeback(method),
            _ => BillingErrors.TransicaoInvalida,
        };
    }

    public Result<Unit> MarcarProcessando(PaymentMethod method)
    {
        if (method is PaymentMethod.Boleto)
            return BillingErrors.MetodoNaoSuporta;
        if (Status != InstallmentStatus.Pendente)
            return BillingErrors.TransicaoInvalida;
        Status = InstallmentStatus.Processando;
        return Unit.Value;
    }

    public Result<Unit> Autorizar(PaymentMethod method)
    {
        if (method != PaymentMethod.Cartao)
            return BillingErrors.MetodoNaoSuporta;
        if (Status != InstallmentStatus.Processando)
            return BillingErrors.TransicaoInvalida;
        Status = InstallmentStatus.Autorizado;
        return Unit.Value;
    }

    public Result<Unit> Recusar(PaymentMethod method)
    {
        if (method != PaymentMethod.Cartao)
            return BillingErrors.MetodoNaoSuporta;
        if (Status != InstallmentStatus.Processando)
            return BillingErrors.TransicaoInvalida;
        Status = InstallmentStatus.Recusado;
        return Unit.Value;
    }

    public Result<Unit> Confirmar(PaymentMethod method, DateTimeOffset agora)
    {
        var ok = method switch
        {
            PaymentMethod.Pix => Status == InstallmentStatus.Processando,
            PaymentMethod.Cartao => Status is InstallmentStatus.Processando or InstallmentStatus.Autorizado,
            PaymentMethod.Boleto => Status == InstallmentStatus.Pendente,
            _ => false,
        };
        if (!ok)
            return BillingErrors.TransicaoInvalida;

        Status = InstallmentStatus.Confirmado;
        ConfirmedAt = agora;
        return Unit.Value;
    }

    public Result<Unit> Expirar(PaymentMethod method)
    {
        var ok = method switch
        {
            PaymentMethod.Pix => Status == InstallmentStatus.Processando,
            PaymentMethod.Cartao => Status is InstallmentStatus.Processando or InstallmentStatus.Autorizado,
            PaymentMethod.Boleto => Status == InstallmentStatus.Pendente,
            _ => false,
        };
        if (!ok)
            return BillingErrors.TransicaoInvalida;

        Status = InstallmentStatus.Expirado;
        return Unit.Value;
    }

    public Result<Unit> Liquidar(PaymentMethod method, DateTimeOffset agora)
    {
        _ = method;
        if (Status != InstallmentStatus.Confirmado)
            return BillingErrors.TransicaoInvalida;
        Status = InstallmentStatus.Liquidado;
        SettledAt = agora;
        return Unit.Value;
    }

    public Result<Unit> Repassar(PaymentMethod method)
    {
        _ = method;
        if (Status != InstallmentStatus.Liquidado)
            return BillingErrors.TransicaoInvalida;
        Status = InstallmentStatus.Repassado;
        return Unit.Value;
    }

    public Result<Unit> SolicitarEstorno(PaymentMethod method)
    {
        if (method == PaymentMethod.Cartao && Status is InstallmentStatus.Chargeback or InstallmentStatus.EmDisputa)
            return BillingErrors.TransicaoInvalida;
        if (Status is not (InstallmentStatus.Confirmado or InstallmentStatus.Liquidado))
            return BillingErrors.TransicaoInvalida;
        Status = InstallmentStatus.EstornoSolicitado;
        return Unit.Value;
    }

    public Result<Unit> ConfirmarEstorno(PaymentMethod method, bool parcial)
    {
        _ = method;
        if (Status != InstallmentStatus.EstornoSolicitado)
            return BillingErrors.TransicaoInvalida;
        Status = parcial ? InstallmentStatus.EstornoParcial : InstallmentStatus.Estornado;
        return Unit.Value;
    }

    public Result<Unit> AbrirChargeback(PaymentMethod method)
    {
        if (method != PaymentMethod.Cartao)
            return BillingErrors.MetodoNaoSuporta;
        if (Status is not (InstallmentStatus.Confirmado or InstallmentStatus.Liquidado))
            return BillingErrors.TransicaoInvalida;
        Status = InstallmentStatus.EmDisputa;
        return Unit.Value;
    }

    public void VincularExterno(string externalId) => ExternalInstallmentId = externalId;
}
