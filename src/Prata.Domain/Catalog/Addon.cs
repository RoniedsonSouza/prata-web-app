using Prata.Domain.Common;

namespace Prata.Domain.Catalog;

/// <summary>
/// Adicional vendavel ligado a um pacote ou tipo de servico.
/// </summary>
public sealed class Addon : AggregateRoot, ITenantOwned
{
    private Addon()
    {
        Name = null!;
    }

    private Addon(Guid id, Guid tenantId, string name, Money price, bool isActive)
        : base(id)
    {
        TenantId = tenantId;
        Name = name;
        Price = price;
        IsActive = isActive;
    }

    public Guid TenantId { get; }

    public string Name { get; private set; }

    public Money Price { get; private set; }

    public bool IsActive { get; private set; }

    public static Result<Addon> Create(Guid tenantId, string name, Money price)
    {
        if (string.IsNullOrWhiteSpace(name))
            return CatalogErrors.NomeObrigatorio;

        if (price.Amount <= 0)
            return CatalogErrors.PrecoInvalido;

        return new Addon(Guid.NewGuid(), tenantId, name.Trim(), price, isActive: true);
    }

    public void Desativar() => IsActive = false;
}
