using Prata.Domain.Common;

namespace Prata.Domain.Sales;

public enum OrderItemKind
{
    Package = 0,
    Addon = 1,
}

/// <summary>
/// Linha do pedido com snapshot de nome e preco (RN-CAT-004).
/// </summary>
public sealed class OrderItem : Entity, ITenantOwned
{
    private OrderItem()
    {
        NameSnapshot = null!;
    }

    private OrderItem(
        Guid id,
        Guid tenantId,
        Guid orderId,
        OrderItemKind kind,
        Guid catalogItemId,
        string nameSnapshot,
        Money unitPriceSnapshot,
        int quantity
    )
        : base(id)
    {
        TenantId = tenantId;
        OrderId = orderId;
        Kind = kind;
        CatalogItemId = catalogItemId;
        NameSnapshot = nameSnapshot;
        UnitPriceSnapshot = unitPriceSnapshot;
        Quantity = quantity;
    }

    public Guid TenantId { get; }

    public Guid OrderId { get; }

    public OrderItemKind Kind { get; }

    public Guid CatalogItemId { get; }

    public string NameSnapshot { get; }

    public Money UnitPriceSnapshot { get; }

    public int Quantity { get; }

    public Money LineTotal => UnitPriceSnapshot.Multiply(Quantity);

    public static Result<OrderItem> Create(
        Guid tenantId,
        Guid orderId,
        OrderItemKind kind,
        Guid catalogItemId,
        string nameSnapshot,
        Money unitPriceSnapshot,
        int quantity
    )
    {
        if (tenantId == Guid.Empty)
            return SalesErrors.TenantInvalido;

        if (orderId == Guid.Empty)
            return SalesErrors.TenantInvalido;

        if (catalogItemId == Guid.Empty)
            return SalesErrors.ItemCatalogoInvalido;

        if (string.IsNullOrWhiteSpace(nameSnapshot))
            return SalesErrors.ItemNomeObrigatorio;

        if (unitPriceSnapshot.Amount < 0)
            return SalesErrors.ItemPrecoInvalido;

        if (quantity <= 0)
            return SalesErrors.ItemQuantidadeInvalida;

        return new OrderItem(
            Guid.NewGuid(),
            tenantId,
            orderId,
            kind,
            catalogItemId,
            nameSnapshot.Trim(),
            unitPriceSnapshot,
            quantity
        );
    }
}
