using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Prata.Application.Abstractions;
using Prata.Domain.Delivery;
using Prata.Domain.Notifications;
using Prata.Infrastructure.Persistence;

namespace Prata.Infrastructure.Jobs;

public interface IExpireGalleriesProcessor
{
    Task<int> ProcessAsync(CancellationToken cancellationToken = default);
}

/// <summary>Avisos 30/7/1 dia e Expirada (RN-ENT-050).</summary>
public sealed class ExpireGalleriesProcessor(
    PrataDbContext db,
    IDateTimeProvider clock,
    INotifier notifier,
    ILogger<ExpireGalleriesProcessor> logger
) : IExpireGalleriesProcessor
{
    public async Task<int> ProcessAsync(CancellationToken cancellationToken = default)
    {
        var agora = clock.UtcNow;
        var galleries = await db
            .Galleries.Where(g =>
                g.Status != GalleryStatus.Expirada
                && g.Status != GalleryStatus.Arquivada
                && g.ExpiresAt <= agora.AddDays(30)
            )
            .ToListAsync(cancellationToken);

        var changed = 0;
        foreach (var g in galleries)
        {
            var days = (g.ExpiresAt - agora).TotalDays;
            if (days <= 0)
            {
                if (g.Expirar().IsSuccess)
                    changed++;
                continue;
            }

            var aviso =
                days <= 1 ? 1
                : days <= 7 ? 7
                : days <= 30 ? 30
                : 0;
            if (aviso == 0)
                continue;

            // Idempotencia por chave de aviso (RN-NOT-001).
            await notifier.SendTransactionalEmailAsync(
                g.TenantId,
                $"gallery-owner+{g.TenantId:N}@prata.local",
                "GaleriaExpirando",
                $"{g.Id:N}:d{aviso}",
                $"Galeria expira em {aviso} dia(s)",
                $"A galeria {g.Id:N} expira em aproximadamente {aviso} dia(s).",
                cancellationToken
            );
        }

        if (changed > 0)
            await db.SaveChangesAsync(cancellationToken);

        if (changed > 0)
            logger.LogInformation("Expirou {Count} galerias", changed);

        return changed;
    }
}
