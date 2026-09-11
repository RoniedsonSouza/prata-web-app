namespace Prata.Domain.Common;

/// <summary>
/// Dinheiro tipado. Nunca double/float (RN-FIN-003). Banco: numeric(14,2).
/// </summary>
public readonly record struct Money : IComparable<Money>
{
    public const int Scale = 2;

    public decimal Amount { get; }

    public Currency Currency { get; }

    private Money(decimal amount, Currency currency)
    {
        Amount = amount;
        Currency = currency;
    }

    public static Money Brl(decimal amount) => Create(amount, Currency.Brl);

    public static Money Zero(Currency currency = Currency.Brl) => new(0m, currency);

    public static Result<Money> TryCreate(decimal amount, Currency currency)
    {
        if (GetScale(amount) > Scale)
            return Error.Validation("DINHEIRO_ESCALA_INVALIDA", "Valor monetario deve ter no maximo 2 casas decimais.");

        return new Money(amount, currency);
    }

    public static Money Create(decimal amount, Currency currency)
    {
        var result = TryCreate(amount, currency);
        if (result.IsFailure)
            throw new ArgumentException(result.Error!.Value.Message, nameof(amount));

        return result.Value;
    }

    public static Result<Money> TryCreatePositive(decimal amount, Currency currency)
    {
        var created = TryCreate(amount, currency);
        if (created.IsFailure)
            return created;

        if (created.Value.Amount <= 0)
            return Error.Validation("DINHEIRO_NAO_POSITIVO", "Valor monetario deve ser maior que zero.");

        return created;
    }

    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount + other.Amount, Currency);
    }

    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount - other.Amount, Currency);
    }

    public Money Multiply(decimal factor) => new(decimal.Round(Amount * factor, Scale, MidpointRounding.ToEven), Currency);

    public Money Percentage(decimal percent) => Multiply(percent / 100m);

    public int CompareTo(Money other)
    {
        EnsureSameCurrency(other);
        return Amount.CompareTo(other.Amount);
    }

    public static bool operator >(Money left, Money right) => left.CompareTo(right) > 0;

    public static bool operator <(Money left, Money right) => left.CompareTo(right) < 0;

    public static bool operator >=(Money left, Money right) => left.CompareTo(right) >= 0;

    public static bool operator <=(Money left, Money right) => left.CompareTo(right) <= 0;

    public static Money operator +(Money left, Money right) => left.Add(right);

    public static Money operator -(Money left, Money right) => left.Subtract(right);

    public override string ToString() =>
        Currency switch
        {
            Currency.Brl => Amount.ToString("C", System.Globalization.CultureInfo.GetCultureInfo("pt-BR")),
            _ => $"{Amount} {Currency}",
        };

    private void EnsureSameCurrency(Money other)
    {
        if (Currency != other.Currency)
            throw new InvalidOperationException($"Nao e possivel operar {Currency} com {other.Currency}.");
    }

    private static int GetScale(decimal value)
    {
        var bits = decimal.GetBits(value);
        return (bits[3] >> 16) & 0xFF;
    }
}
