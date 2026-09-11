using Microsoft.EntityFrameworkCore;
using Prata.Application.Sales;
using Prata.Domain.Catalog;
using Prata.Domain.Tenancy;

namespace Prata.Infrastructure.Persistence;

internal sealed class CatalogReader(PrataDbContext db) : ICatalogReader
{
    public Task<ServiceType?> GetServiceTypeAsync(
        Guid tenantId,
        Guid serviceTypeId,
        CancellationToken cancellationToken
    ) =>
        db.ServiceTypes.FirstOrDefaultAsync(
            s => s.TenantId == tenantId && s.Id == serviceTypeId,
            cancellationToken
        );

    public Task<Package?> GetPackageAsync(Guid tenantId, Guid packageId, CancellationToken cancellationToken) =>
        db.Packages.FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Id == packageId, cancellationToken);

    public Task<Addon?> GetAddonAsync(Guid tenantId, Guid addonId, CancellationToken cancellationToken) =>
        db.Addons.FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Id == addonId, cancellationToken);

    public async Task<IReadOnlyList<Package>> ListPublishedPackagesAsync(
        Guid tenantId,
        Guid serviceTypeId,
        CancellationToken cancellationToken
    )
    {
        return await db
            .Packages.AsNoTracking()
            .Where(p =>
                p.TenantId == tenantId
                && p.ServiceTypeId == serviceTypeId
                && p.Status == PackageStatus.Publicado
            )
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Addon>> ListActiveAddonsAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        return await db
            .Addons.AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.IsActive)
            .OrderBy(a => a.Name)
            .ToListAsync(cancellationToken);
    }
}

internal sealed class TenantReader(PrataDbContext db) : ITenantReader
{
    public async Task<int?> GetQuoteValidityDaysAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var tenant = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);
        return tenant?.Settings.QuoteValidityDays;
    }
}
