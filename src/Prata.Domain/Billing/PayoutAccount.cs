using Prata.Domain.Common;

namespace Prata.Domain.Billing;

/// <summary>Conta de recebimento no PSP — so identificador e mascarados (RN-FIN-002).</summary>
public sealed class PayoutAccount : AggregateRoot, ITenantOwned
{
    private PayoutAccount()
    {
        ExternalRecipientId = null!;
    }

    private PayoutAccount(
        Guid id,
        Guid tenantId,
        string externalRecipientId,
        KycStatus kycStatus,
        string? holderDocumentMasked,
        string? pixKeyMasked,
        string? bankMasked,
        DateTimeOffset createdAt
    )
        : base(id)
    {
        TenantId = tenantId;
        ExternalRecipientId = externalRecipientId;
        KycStatus = kycStatus;
        HolderDocumentMasked = holderDocumentMasked;
        PixKeyMasked = pixKeyMasked;
        BankMasked = bankMasked;
        CreatedAt = createdAt;
        KycUpdatedAt = createdAt;
    }

    public Guid TenantId { get; }

    public string ExternalRecipientId { get; private set; }

    public KycStatus KycStatus { get; private set; }

    public string? HolderDocumentMasked { get; private set; }

    public string? PixKeyMasked { get; private set; }

    public string? BankMasked { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset KycUpdatedAt { get; private set; }

    public static Result<PayoutAccount> Create(
        Guid tenantId,
        string externalRecipientId,
        string? holderDocumentMasked,
        string? pixKeyMasked,
        string? bankMasked,
        DateTimeOffset agora
    )
    {
        if (string.IsNullOrWhiteSpace(externalRecipientId))
            return Error.Validation("PAYOUT_ACCOUNT_REF_OBRIGATORIA", "Identificador do recebedor no PSP e obrigatorio.");

        return new PayoutAccount(
            Guid.NewGuid(),
            tenantId,
            externalRecipientId.Trim(),
            KycStatus.Pendente,
            holderDocumentMasked,
            pixKeyMasked,
            bankMasked,
            agora
        );
    }

    public Result<Unit> AtualizarKyc(KycStatus status, DateTimeOffset agora)
    {
        KycStatus = status;
        KycUpdatedAt = agora;
        return Unit.Value;
    }
}

public sealed class Payout : AggregateRoot, ITenantOwned
{
    private Payout()
    {
        Amount = Money.Zero();
    }

    private Payout(
        Guid id,
        Guid tenantId,
        Guid paymentId,
        Money amount,
        PayoutStatus status,
        DateTimeOffset scheduledAt
    )
        : base(id)
    {
        TenantId = tenantId;
        PaymentId = paymentId;
        Amount = amount;
        Status = status;
        ScheduledAt = scheduledAt;
    }

    public Guid TenantId { get; }

    public Guid PaymentId { get; }

    public Money Amount { get; }

    public PayoutStatus Status { get; private set; }

    public DateTimeOffset ScheduledAt { get; private set; }

    public DateTimeOffset? SettledAt { get; private set; }

    public string? FailureReason { get; private set; }

    public string? ExternalPayoutId { get; private set; }

    public static Result<Payout> AgendarOuBloquear(
        Guid tenantId,
        Guid paymentId,
        Money amount,
        KycStatus kyc,
        DateTimeOffset agora
    )
    {
        if (amount.Amount <= 0)
            return Error.Validation("PAYOUT_VALOR_INVALIDO", "Valor do repasse deve ser positivo.");

        var status = kyc == KycStatus.Aprovado ? PayoutStatus.Agendado : PayoutStatus.BloqueadoKyc;
        return new Payout(Guid.NewGuid(), tenantId, paymentId, amount, status, agora);
    }

    public Result<Unit> LiberarAposKyc(DateTimeOffset agora)
    {
        if (Status != PayoutStatus.BloqueadoKyc)
            return BillingErrors.PayoutNaoBloqueado;
        Status = PayoutStatus.Agendado;
        ScheduledAt = agora;
        return Unit.Value;
    }

    public Result<Unit> MarcarEmTransito(string externalPayoutId)
    {
        if (Status != PayoutStatus.Agendado)
            return Error.Validation("PAYOUT_TRANSICAO_INVALIDA", "So Agendado vai para EmTransito.");
        Status = PayoutStatus.EmTransito;
        ExternalPayoutId = externalPayoutId;
        return Unit.Value;
    }

    public Result<Unit> MarcarLiquidado(DateTimeOffset agora)
    {
        if (Status != PayoutStatus.EmTransito)
            return Error.Validation("PAYOUT_TRANSICAO_INVALIDA", "So EmTransito vai para Liquidado.");
        Status = PayoutStatus.Liquidado;
        SettledAt = agora;
        return Unit.Value;
    }
}
