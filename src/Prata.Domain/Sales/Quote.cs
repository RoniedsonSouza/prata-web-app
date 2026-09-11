using Prata.Domain.Common;

namespace Prata.Domain.Sales;

/// <summary>
/// Versao de orcamento. ValidoAte e gravado, nao calculado na leitura (RN-COM-013).
/// </summary>
public sealed class Quote : Entity, ITenantOwned
{
    private Quote() { }

    private Quote(
        Guid id,
        Guid tenantId,
        Guid orderId,
        int version,
        DateTimeOffset emittedAt,
        DateTimeOffset validoAte,
        Money totalSnapshot
    )
        : base(id)
    {
        TenantId = tenantId;
        OrderId = orderId;
        Version = version;
        EmittedAt = emittedAt;
        ValidoAte = validoAte;
        TotalSnapshot = totalSnapshot;
    }

    public Guid TenantId { get; }

    public Guid OrderId { get; }

    public int Version { get; }

    public DateTimeOffset EmittedAt { get; }

    public DateTimeOffset ValidoAte { get; }

    public Money TotalSnapshot { get; }

    public bool EstaVigente(DateTimeOffset agora) => agora <= ValidoAte;

    public static Result<Quote> Create(
        Guid tenantId,
        Guid orderId,
        int version,
        DateTimeOffset emittedAt,
        int validadeDias,
        Money totalSnapshot
    )
    {
        if (tenantId == Guid.Empty || orderId == Guid.Empty)
            return SalesErrors.TenantInvalido;

        if (version < 1)
            return SalesErrors.OrcamentoVersaoInvalida;

        if (validadeDias < 1)
            return SalesErrors.OrcamentoValidadeInvalida;

        if (totalSnapshot.Amount <= 0)
            return SalesErrors.OrcamentoTotalInvalido;

        return new Quote(
            Guid.NewGuid(),
            tenantId,
            orderId,
            version,
            emittedAt,
            emittedAt.AddDays(validadeDias),
            totalSnapshot
        );
    }
}
