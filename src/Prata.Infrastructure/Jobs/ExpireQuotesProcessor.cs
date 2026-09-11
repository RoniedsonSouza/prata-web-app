using Microsoft.EntityFrameworkCore;
using Prata.Application.Abstractions;
using Prata.Application.Sales;
using Prata.Domain.Notifications;
using Prata.Domain.Sales;
using Prata.Domain.Tenancy;
using Prata.Infrastructure.Persistence;

namespace Prata.Infrastructure.Jobs;

/// <summary>
/// Job diario de expiracao de orcamento (E2). Processa por tenant (payload/contexto).
/// Tambem avisa orcamentos a expirar em 24h (RN-NOT-001).
/// </summary>
public interface IExpireQuotesProcessor
{
    Task<int> ExpirarAsync(CancellationToken cancellationToken = default);
}

public sealed class ExpireQuotesProcessor(
    PrataDbContext db,
    IDateTimeProvider clock,
    MutableTenantContext tenantContext,
    INotifier notifier,
    IClientRepository clients
) : IExpireQuotesProcessor
{
    public async Task<int> ExpirarAsync(CancellationToken cancellationToken = default)
    {
        var agora = clock.UtcNow;
        var candidates = await db
            .Orders.IgnoreQueryFilters()
            .Include("_quotes")
            .Where(o => o.Status == OrderStatus.OrcamentoEnviado)
            .ToListAsync(cancellationToken);

        var expired = 0;
        foreach (var group in candidates.GroupBy(o => o.TenantId))
        {
            tenantContext.Set(group.Key, "job-expirar-orcamentos", TenantStatus.Ativo);
            await db.Database.OpenConnectionAsync(cancellationToken);
            await db.Database.ExecuteSqlRawAsync(
                "SELECT set_config('prata.tenant_id', {0}, true)",
                group.Key.ToString()
            );

            foreach (var order in group)
            {
                if (order.CurrentQuote is null)
                    continue;

                var quote = order.CurrentQuote;
                var hoursLeft = (quote.ValidoAte - agora).TotalHours;
                if (hoursLeft is > 0 and <= 24)
                {
                    await CommercialNotification.TrySendAsync(
                        notifier,
                        clients,
                        order.TenantId,
                        order.ClientId,
                        NotificationTypes.OrcamentoExpirando,
                        $"{order.Id:N}:v{quote.Version}:expiring",
                        "Seu orcamento expira em breve",
                        $"O orcamento v{quote.Version} vence em {quote.ValidoAte:dd/MM/yyyy}. Aprove no portal se ainda fizer sentido.",
                        cancellationToken
                    );
                }

                if (quote.EstaVigente(agora))
                    continue;

                if (order.Expirar(agora).IsSuccess)
                    expired++;
            }

            await db.SaveChangesAsync(cancellationToken);
        }

        return expired;
    }
}
