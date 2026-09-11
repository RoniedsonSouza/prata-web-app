using Prata.Domain.Common;

namespace Prata.Domain.Billing;

/// <summary>Regra de comissao versionada (RN-FIN-033). Sem UPDATE destrutivo.</summary>
public sealed class SplitRule : AggregateRoot, ITenantOwned
{
    private SplitRule()
    {
    }

    private SplitRule(
        Guid id,
        Guid tenantId,
        decimal percent,
        Money? fixedAmount,
        DateTimeOffset vigenteDe,
        DateTimeOffset? vigenteAte,
        Guid createdBy
    )
        : base(id)
    {
        TenantId = tenantId;
        Percent = percent;
        FixedAmount = fixedAmount;
        VigenteDe = vigenteDe;
        VigenteAte = vigenteAte;
        CreatedBy = createdBy;
    }

    public Guid TenantId { get; }

    public decimal Percent { get; }

    public Money? FixedAmount { get; }

    public DateTimeOffset VigenteDe { get; private set; }

    public DateTimeOffset? VigenteAte { get; private set; }

    public Guid CreatedBy { get; }

    public static Result<SplitRule> Create(
        Guid tenantId,
        decimal percent,
        Money? fixedAmount,
        DateTimeOffset vigenteDe,
        Guid createdBy
    )
    {
        if (percent < 0 || percent > 100)
            return BillingErrors.PercentInvalido;

        return new SplitRule(Guid.NewGuid(), tenantId, percent, fixedAmount, vigenteDe, null, createdBy);
    }

    public (SplitRule Encerrada, SplitRule Nova) Substituir(decimal novoPercent, DateTimeOffset agora, Guid createdBy)
    {
        VigenteAte = agora;
        var nova = Create(TenantId, novoPercent, FixedAmount, agora, createdBy).Value;
        return (this, nova);
    }

    public SplitRuleSnapshot CriarSnapshot(Money? baseAmount = null)
    {
        var fee =
            baseAmount is null
                ? Money.Zero()
                : baseAmount.Value.Percentage(Percent).Add(FixedAmount ?? Money.Zero(baseAmount.Value.Currency));
        return new SplitRuleSnapshot(Percent, FixedAmount, fee);
    }
}

/// <summary>Snapshot imutavel gravado no Payment (RN-FIN-032).</summary>
public sealed class SplitRuleSnapshot : ValueObject
{
    public SplitRuleSnapshot(decimal percent, Money? fixedAmount, Money platformFee)
    {
        Percent = percent;
        FixedAmount = fixedAmount;
        PlatformFee = platformFee;
    }

    public decimal Percent { get; }

    public Money? FixedAmount { get; }

    public Money PlatformFee { get; }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Percent;
        yield return FixedAmount;
        yield return PlatformFee;
    }
}
