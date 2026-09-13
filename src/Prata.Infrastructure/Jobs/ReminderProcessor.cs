using Microsoft.EntityFrameworkCore;
using Prata.Application.Abstractions;
using Prata.Domain.Billing;
using Prata.Domain.Contracts;
using Prata.Domain.Notifications;
using Prata.Domain.Sales;
using Prata.Infrastructure.Persistence;

namespace Prata.Infrastructure.Jobs;

/// <summary>Lembretes de assinatura em 1, 3 e 7 dias (RN-CTR-020) e de saldo (RN-NOT-020).</summary>
public interface IReminderProcessor
{
    Task<int> ProcessAsync(CancellationToken cancellationToken = default);
}

public sealed class ReminderProcessor(PrataDbContext db, INotifier notifier, IDateTimeProvider clock) : IReminderProcessor
{
    private static readonly int[] ReminderDays = [1, 3, 7];

    public async Task<int> ProcessAsync(CancellationToken cancellationToken = default)
    {
        var sent = 0;
        sent += await LembrarAssinaturasAsync(cancellationToken);
        sent += await LembrarSaldosAsync(cancellationToken);
        return sent;
    }

    private async Task<int> LembrarAssinaturasAsync(CancellationToken cancellationToken)
    {
        var agora = clock.UtcNow;
        var payments = await db
            .Payments.AsNoTracking()
            .Where(p => p.Kind == PaymentKind.Deposit)
            .ToListAsync(cancellationToken);

        var count = 0;
        foreach (var payment in payments.Where(p => p.SinalConfirmado))
        {
            var order = await db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == payment.OrderId, cancellationToken);
            if (order is null || order.Status != OrderStatus.Aprovado)
                continue;

            var signed = await db.Contracts.AnyAsync(
                c => c.TenantId == payment.TenantId && c.OrderId == order.Id && c.Status == ContractStatus.Assinado,
                cancellationToken
            );
            if (signed)
                continue;

            var client = await db.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Id == order.ClientId, cancellationToken);
            if (client is null)
                continue;

            var contract = await db
                .Contracts.AsNoTracking()
                .Where(c => c.TenantId == payment.TenantId && c.OrderId == order.Id && c.SentAt != null)
                .OrderByDescending(c => c.SentAt)
                .FirstOrDefaultAsync(cancellationToken);
            if (contract?.SentAt is null)
                continue;

            var daysSince = (agora.Date - contract.SentAt.Value.UtcDateTime.Date).Days;
            foreach (var day in ReminderDays.Where(d => d == daysSince))
            {
                await notifier.SendTransactionalEmailAsync(
                    payment.TenantId,
                    client.Email,
                    NotificationTypes.LembreteAssinatura,
                    $"assinatura:{order.Id}:d{day}",
                    "Lembrete: assine o contrato",
                    "Ha um contrato pendente de assinatura. Acesse o portal com o link autenticado.",
                    cancellationToken
                );
                count++;
            }
        }

        return count;
    }

    private async Task<int> LembrarSaldosAsync(CancellationToken cancellationToken)
    {
        var hoje = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var balances = await db
            .Payments.AsNoTracking()
            .Include("_installments")
            .Where(p => p.Kind == PaymentKind.Balance)
            .ToListAsync(cancellationToken);

        var count = 0;
        foreach (var payment in balances)
        {
            if (payment.Status is PaymentStatus.Confirmado or PaymentStatus.Liquidado)
                continue;

            var due = payment.Installments.OrderBy(i => i.Sequence).FirstOrDefault()?.DueDate;
            if (due is null)
                continue;

            var daysUntil = due.Value.DayNumber - hoje.DayNumber;
            if (!ReminderDays.Contains(daysUntil))
                continue;

            var order = await db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == payment.OrderId, cancellationToken);
            if (order is null)
                continue;
            var client = await db.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Id == order.ClientId, cancellationToken);
            if (client is null)
                continue;

            await notifier.SendTransactionalEmailAsync(
                payment.TenantId,
                client.Email,
                NotificationTypes.LembreteSaldo,
                $"saldo:{payment.Id}:d{daysUntil}",
                "Lembrete: saldo do ensaio",
                "Ha um saldo pendente. Acesse o portal com o link autenticado para pagar.",
                cancellationToken
            );
            count++;
        }

        return count;
    }
}
