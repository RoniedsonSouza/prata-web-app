using Prata.Domain.Common;

namespace Prata.Domain.Billing;

/// <summary>
/// Agregado de cobranca. Estado derivado das parcelas (docs/05 §3.4).
/// Split snapshot imutavel (RN-FIN-032).
/// </summary>
public sealed class Payment : AggregateRoot, ITenantOwned
{
    private readonly List<Installment> _installments = [];

    private Payment()
    {
        Total = Money.Zero();
        PlatformFeeAmount = Money.Zero();
        SplitSnapshot = null!;
    }

    private Payment(
        Guid id,
        Guid tenantId,
        Guid orderId,
        PaymentKind kind,
        Money total,
        PaymentMethod method,
        SplitRuleSnapshot splitSnapshot,
        DateTimeOffset createdAt
    )
        : base(id)
    {
        TenantId = tenantId;
        OrderId = orderId;
        Kind = kind;
        Total = total;
        Method = method;
        SplitSnapshot = splitSnapshot;
        PlatformFeeAmount = splitSnapshot.PlatformFee;
        Status = PaymentStatus.Pendente;
        CreatedAt = createdAt;
    }

    public Guid TenantId { get; }

    public Guid OrderId { get; }

    public PaymentKind Kind { get; }

    public PaymentStatus Status { get; private set; }

    public Money Total { get; }

    public PaymentMethod Method { get; }

    public SplitRuleSnapshot SplitSnapshot { get; }

    public Money PlatformFeeAmount { get; }

    public string? ExternalChargeId { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public IReadOnlyList<Installment> Installments => _installments;

    public static Result<Payment> Create(
        Guid tenantId,
        Guid orderId,
        PaymentKind kind,
        Money total,
        PaymentMethod method,
        SplitRuleSnapshot? splitSnapshot,
        decimal depositPercent,
        DateOnly balanceDueDate,
        DateTimeOffset agora
    )
    {
        if (splitSnapshot is null)
            return BillingErrors.SplitObrigatorio;

        if (total.Amount <= 0)
            return Error.Validation("PAGAMENTO_TOTAL_INVALIDO", "Total do pagamento deve ser positivo.");

        var depositCheck = DepositPolicy.ValidatePercent(depositPercent);
        if (depositCheck.IsFailure)
            return Result.Failure<Payment>(depositCheck.Error!.Value);

        var payment = new Payment(
            Guid.NewGuid(),
            tenantId,
            orderId,
            kind,
            total,
            method,
            splitSnapshot,
            agora
        );

        // Sinal + saldo (modelo padrao E3). Kind Deposit ainda carrega o plano completo.
        var deposit = total.Percentage(depositPercent);
        var balance = total.Subtract(deposit);
        var dueSinal = DateOnly.FromDateTime(agora.UtcDateTime.Date);
        var i1 = Installment.Create(tenantId, payment.Id, 1, deposit, dueSinal, method);
        var i2 = Installment.Create(tenantId, payment.Id, 2, balance, balanceDueDate, method);
        if (i1.IsFailure)
            return Result.Failure<Payment>(i1.Error!.Value);
        if (i2.IsFailure)
            return Result.Failure<Payment>(i2.Error!.Value);

        payment._installments.Add(i1.Value);
        payment._installments.Add(i2.Value);

        var sum = payment._installments.Aggregate(Money.Zero(total.Currency), (a, i) => a.Add(i.Amount));
        if (sum.Amount != total.Amount)
            return BillingErrors.ParcelasNaoSomam;

        return payment;
    }

    public Result<Unit> RecalcularStatus()
    {
        if (_installments.Count == 0)
        {
            Status = PaymentStatus.Pendente;
            return Unit.Value;
        }

        var confirmed = _installments.Count(i =>
            i.Status
                is InstallmentStatus.Confirmado
                    or InstallmentStatus.Liquidado
                    or InstallmentStatus.Repassado
        );
        var liquidated = _installments.Count(i =>
            i.Status is InstallmentStatus.Liquidado or InstallmentStatus.Repassado
        );

        if (liquidated == _installments.Count)
            Status = PaymentStatus.Liquidado;
        else if (confirmed == _installments.Count)
            Status = PaymentStatus.Confirmado;
        else if (confirmed > 0)
            Status = PaymentStatus.ParcialmentePago;
        else
            Status = PaymentStatus.Pendente;

        return Unit.Value;
    }

    public void VincularCobrancaExterna(string externalChargeId) => ExternalChargeId = externalChargeId;

    /// <summary>Sinal confirmado = primeira parcela Confirmado+.</summary>
    public bool SinalConfirmado =>
        _installments.Count > 0
        && _installments[0].Status
            is InstallmentStatus.Confirmado
                or InstallmentStatus.Liquidado
                or InstallmentStatus.Repassado;
}
