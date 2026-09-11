using Prata.Domain.Common;

namespace Prata.Domain.Catalog;

/// <summary>
/// Tipo de servico do catalogo. Seed dos dez tipos na criacao do tenant.
/// </summary>
public sealed class ServiceType : AggregateRoot, ITenantOwned
{
    private ServiceType()
    {
        Code = null!;
        Name = null!;
    }

    private ServiceType(Guid id, Guid tenantId, string code, string name, int sortOrder, bool isActive)
        : base(id)
    {
        TenantId = tenantId;
        Code = code;
        Name = name;
        SortOrder = sortOrder;
        IsActive = isActive;
    }

    public Guid TenantId { get; }

    public string Code { get; }

    public string Name { get; }

    public int SortOrder { get; }

    public bool IsActive { get; private set; }

    public static ServiceType Create(Guid tenantId, string code, string name, int sortOrder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new ServiceType(Guid.NewGuid(), tenantId, code, name.Trim(), sortOrder, isActive: true);
    }

    public void Desativar() => IsActive = false;

    public void Ativar() => IsActive = true;

    /// <summary>
    /// Seed padrao na criacao do tenant (docs/04-MODULOS-E-AGREGADOS.md §02).
    /// </summary>
    public static IReadOnlyList<ServiceType> CreateDefaultSeed(Guid tenantId)
    {
        var definitions = new (string Code, string Name)[]
        {
            ("casamento-civil", "Casamento civil"),
            ("pre-wedding", "Pre-wedding"),
            ("aniversario-infantil", "Aniversario infantil"),
            ("studio", "Studio"),
            ("corporativo", "Corporativo"),
            ("gestante", "Gestante"),
            ("newborn", "Newborn"),
            ("formatura", "Formatura"),
            ("book-15-anos", "Book 15 anos"),
            ("ensaio-familia", "Ensaio de familia"),
        };

        return definitions.Select((d, i) => Create(tenantId, d.Code, d.Name, i + 1)).ToList();
    }
}
