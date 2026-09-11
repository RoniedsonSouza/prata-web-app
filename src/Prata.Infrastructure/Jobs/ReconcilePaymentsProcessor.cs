using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Prata.Application.Abstractions;
using Prata.Domain.Billing;
using Prata.Domain.Tenancy;
using Prata.Infrastructure.Persistence;

namespace Prata.Infrastructure.Jobs;

public interface IReconcilePaymentsProcessor
{
    Task<int> ProcessAsync(CancellationToken cancellationToken = default);
}

/// <summary>RN-FIN-050 — detecta divergencia; nunca resolve automaticamente (RN-FIN-051).</summary>
public sealed class ReconcilePaymentsProcessor(
    PrataDbContext db,
    IPaymentGateway gateway,
    IDateTimeProvider clock,
    MutableTenantContext tenantContext,
    ILogger<ReconcilePaymentsProcessor> logger
) : IReconcilePaymentsProcessor
{
    public async Task<int> ProcessAsync(CancellationToken cancellationToken = default)
    {
        var ate = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime.Date);
        var de = ate.AddDays(-1);
        var extrato = await gateway.ObterExtratoAsync(de, ate, cancellationToken);
        if (extrato.IsFailure)
        {
            logger.LogWarning("Conciliação: falha ao obter extrato {Code}", extrato.Error!.Value.Code);
            return 0;
        }

        var opened = 0;
        foreach (var line in extrato.Value)
        {
            var payment = await db
                .Payments.IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.ExternalChargeId == line.ExternalChargeId, cancellationToken);

            if (payment is null)
            {
                // Sem tenant resolvido, nao grava com Guid.Empty (RLS + RN-TEN-001).
                logger.LogWarning(
                    "Conciliação: cobranca {ChargeId} ausente localmente — divergencia nao persistida",
                    line.ExternalChargeId
                );
                continue;
            }

            var statusMismatch = !string.Equals(payment.Status.ToString(), line.Status, StringComparison.OrdinalIgnoreCase);
            var amountMismatch = payment.Total.Amount != line.Amount.Amount;
            if (!statusMismatch && !amountMismatch)
                continue;

            tenantContext.Set(payment.TenantId, "job-reconcile-payments", TenantStatus.Ativo);
            await db.Database.OpenConnectionAsync(cancellationToken);
            await db.Database.ExecuteSqlRawAsync("SELECT set_config('prata.tenant_id', {0}, true)", payment.TenantId.ToString());

            db.ReconciliationIssues.Add(
                ReconciliationIssue.Open(
                    payment.TenantId,
                    payment.Id,
                    "ESTADO_OU_VALOR_DIVERGENTE",
                    expectedJson: $"{{\"status\":\"{payment.Status}\",\"amount\":{payment.Total.Amount}}}",
                    foundJson: $"{{\"status\":\"{line.Status}\",\"amount\":{line.Amount.Amount}}}",
                    clock.UtcNow
                )
            );
            opened++;
        }

        if (opened > 0)
            await db.SaveChangesAsync(cancellationToken);

        return opened;
    }
}
