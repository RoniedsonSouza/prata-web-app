using Microsoft.EntityFrameworkCore;
using Prata.Application.Abstractions;
using Prata.Domain.Sales;

namespace Prata.Infrastructure.Persistence;

internal sealed class ClientRepository(PrataDbContext db) : IClientRepository
{
    public Task<Client?> GetByEmailAsync(Guid tenantId, string email, CancellationToken cancellationToken = default)
    {
        var normalized = Client.NormalizeEmail(email) ?? email.Trim().ToLowerInvariant();
        return db.Clients.FirstOrDefaultAsync(
            c => c.TenantId == tenantId && c.Email == normalized,
            cancellationToken
        );
    }

    public Task<Client?> GetByIdAsync(Guid tenantId, Guid clientId, CancellationToken cancellationToken = default) =>
        db.Clients.FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == clientId, cancellationToken);

    public async Task AddAsync(Client client, CancellationToken cancellationToken = default)
    {
        await db.Clients.AddAsync(client, cancellationToken);
    }
}

internal sealed class OrderRepository(PrataDbContext db) : IOrderRepository
{
    public Task<Order?> GetByIdAsync(Guid tenantId, Guid orderId, CancellationToken cancellationToken = default) =>
        db
            .Orders.Include("_items")
            .Include("_quotes")
            .FirstOrDefaultAsync(o => o.TenantId == tenantId && o.Id == orderId, cancellationToken);

    public async Task AddAsync(Order order, CancellationToken cancellationToken = default)
    {
        await db.Orders.AddAsync(order, cancellationToken);
    }

    public async Task<IReadOnlyList<DateOnly>> GetConfirmedIntendedDatesAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default
    )
    {
        return await db
            .Orders.AsNoTracking()
            .Where(o => o.TenantId == tenantId && o.Status == OrderStatus.Confirmado)
            .Select(o => o.IntendedDate)
            .ToListAsync(cancellationToken);
    }
}
