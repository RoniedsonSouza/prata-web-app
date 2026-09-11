using Prata.Domain.Tenancy;

namespace Prata.Application.Abstractions;

/// <summary>
/// Contexto do tenant resolvido pelo middleware TenantResolution.
/// </summary>
public interface ITenantContext
{
    Guid? TenantId { get; }

    string? Slug { get; }

    TenantStatus? Status { get; }

    bool IsResolved { get; }
}

public interface ITenantContextAccessor
{
    ITenantContext Current { get; }

    void Set(ITenantContext context);
}

public sealed class MutableTenantContext : ITenantContext, ITenantContextAccessor
{
    public Guid? TenantId { get; private set; }

    public string? Slug { get; private set; }

    public TenantStatus? Status { get; private set; }

    public bool IsResolved => TenantId.HasValue;

    public ITenantContext Current => this;

    public void Set(ITenantContext context)
    {
        TenantId = context.TenantId;
        Slug = context.Slug;
        Status = context.Status;
    }

    public void Set(Guid tenantId, string slug, TenantStatus status)
    {
        TenantId = tenantId;
        Slug = slug;
        Status = status;
    }

    public void Clear()
    {
        TenantId = null;
        Slug = null;
        Status = null;
    }
}
