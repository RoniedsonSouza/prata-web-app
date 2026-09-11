using Prata.Domain.Common;

namespace Prata.Domain.Sales;

public enum DiscountKind
{
    Fixed = 0,
    Percent = 1,
}

/// <summary>
/// Desconto do pedido. Percentual e fixo nao se acumulam (RN-COM-002).
/// </summary>
public sealed class Discount : ValueObject
{
    // EF Core
    private Discount() { }

    private Discount(DiscountKind kind, Money? fixedAmount, decimal? percent)
    {
        Kind = kind;
        FixedAmount = fixedAmount;
        Percent = percent;
    }

    public DiscountKind Kind { get; }

    public Money? FixedAmount { get; }

    public decimal? Percent { get; }

    public static Discount Fixed(Money amount)
    {
        if (amount.Amount <= 0)
            throw new ArgumentException("Desconto fixo deve ser positivo.", nameof(amount));

        return new Discount(DiscountKind.Fixed, amount, null);
    }

    public static Result<Discount> TryFixed(Money amount)
    {
        if (amount.Amount <= 0)
            return SalesErrors.DescontoInvalido;

        return Fixed(amount);
    }

    public static Result<Discount> TryPercent(decimal percent)
    {
        if (percent <= 0 || percent > 100)
            return SalesErrors.DescontoInvalido;

        return new Discount(DiscountKind.Percent, null, percent);
    }

    public Money ApplyTo(Money subtotal)
    {
        return Kind switch
        {
            DiscountKind.Fixed => FixedAmount!.Value,
            DiscountKind.Percent => subtotal.Percentage(Percent!.Value),
            _ => Money.Zero(subtotal.Currency),
        };
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Kind;
        yield return FixedAmount;
        yield return Percent;
    }
}
