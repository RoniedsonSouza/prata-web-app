using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Prata.Application.Abstractions;
using Prata.Application.Billing;
using Prata.Domain.Billing;
using Prata.Domain.Common;
using Prata.Domain.Sales;
using Prata.Infrastructure.Persistence;

namespace Prata.Infrastructure.Billing;

public sealed class DepositPaymentService(
    IOrderRepository orders,
    IPaymentGateway gateway,
    IDateTimeProvider clock,
    IConfiguration configuration,
    PrataDbContext db,
    IUnitOfWork uow
) : IDepositPaymentService
{
    public async Task<Result<Guid>> CreateForApprovedOrderAsync(Guid tenantId, Guid orderId, CancellationToken cancellationToken = default)
    {
        var order = await orders.GetByIdAsync(tenantId, orderId, cancellationToken);
        if (order is null)
            return Error.Validation("PEDIDO_NAO_ENCONTRADO", "Pedido nao encontrado.");

        if (order.Status != OrderStatus.Aprovado)
            return Error.Validation("PEDIDO_NAO_APROVADO", "So cria sinal apos orcamento aprovado.");

        var existing = await db.Payments.FirstOrDefaultAsync(
            p => p.TenantId == tenantId && p.OrderId == order.Id && p.Kind == PaymentKind.Deposit,
            cancellationToken
        );
        if (existing is not null)
            return existing.Id;

        var feePercent = ParseDecimal(configuration["Payments:DefaultPlatformFeePercent"], 8m);
        var depositPercent = ParseDecimal(configuration["Payments:DefaultDepositPercent"], 40m);
        var balanceDueRaw = configuration["Payments:BalanceDueDays"] ?? configuration["Payments:BalanceDueDaysBeforeEvent"];
        var balanceDueDays = int.TryParse(balanceDueRaw, out var d) ? d : 7;

        var rule = await db
            .SplitRules.Where(r => r.TenantId == tenantId && r.VigenteAte == null)
            .OrderByDescending(r => r.VigenteDe)
            .FirstOrDefaultAsync(cancellationToken);

        if (rule is null)
        {
            var createdRule = SplitRule.Create(tenantId, feePercent, null, clock.UtcNow, Guid.Empty);
            if (createdRule.IsFailure)
                return Result.Failure<Guid>(createdRule.Error!.Value);
            rule = createdRule.Value;
            db.SplitRules.Add(rule);
        }

        var snap = rule.CriarSnapshot(order.Total);
        var balanceDue = order.IntendedDate.AddDays(-balanceDueDays);
        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime.Date);
        if (balanceDue < today)
            balanceDue = today.AddDays(1);

        var payment = Payment.Create(
            tenantId,
            order.Id,
            PaymentKind.Deposit,
            order.Total,
            PaymentMethod.Pix,
            snap,
            depositPercent,
            balanceDue,
            clock.UtcNow
        );
        if (payment.IsFailure)
            return Result.Failure<Guid>(payment.Error!.Value);

        var payoutAccount = await db.PayoutAccounts.FirstOrDefaultAsync(a => a.TenantId == tenantId, cancellationToken);
        var recipientId = payoutAccount?.ExternalRecipientId ?? $"pending-{tenantId:N}";

        var depositAmount = payment.Value.Installments[0].Amount;
        var depositFee = depositAmount.Percentage(snap.Percent);
        var charge = await gateway.CriarCobrancaAsync(
            new ChargeRequest(
                tenantId,
                payment.Value.Id,
                depositAmount,
                "Pix",
                snap.Percent,
                depositFee,
                recipientId,
                IdempotencyKey: $"deposit:{order.Id:N}",
                CreditCardToken: null
            ),
            cancellationToken
        );
        if (charge.IsFailure)
            return Result.Failure<Guid>(charge.Error!.Value);

        payment.Value.VincularCobrancaExterna(charge.Value.ExternalChargeId);
        payment.Value.Installments[0].MarcarProcessando(PaymentMethod.Pix);
        payment.Value.Installments[0].VincularExterno(charge.Value.ExternalChargeId);

        db.Payments.Add(payment.Value);
        await uow.SaveChangesAsync(cancellationToken);
        return payment.Value.Id;
    }

    private static decimal ParseDecimal(string? raw, decimal fallback) =>
        decimal.TryParse(raw, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var v)
            ? v
            : fallback;
}

public sealed class OrderConfirmationService(IOrderRepository orders, IDateTimeProvider clock, PrataDbContext db, IUnitOfWork uow)
    : IOrderConfirmationService
{
    public async Task<Result<Unit>> ConfirmAsync(
        Guid tenantId,
        Guid orderId,
        bool contratoAssinado,
        CancellationToken cancellationToken = default
    )
    {
        var order = await orders.GetByIdAsync(tenantId, orderId, cancellationToken);
        if (order is null)
            return Error.Validation("PEDIDO_NAO_ENCONTRADO", "Pedido nao encontrado.");

        var payment = await db
            .Payments.Include("_installments")
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.OrderId == order.Id, cancellationToken);

        var sinalOk = payment?.SinalConfirmado == true;
        var contratoOk =
            contratoAssinado
            || await db.Contracts.AnyAsync(
                c => c.TenantId == tenantId && c.OrderId == order.Id && c.Status == Domain.Contracts.ContractStatus.Assinado,
                cancellationToken
            );
        var result = order.Confirmar(sinalOk, contratoOk, clock.UtcNow);
        if (result.IsFailure)
            return result;

        var day = order.IntendedDate;
        var blocked = await db.BlackoutDates.AnyAsync(b => b.TenantId == tenantId && b.Date == day, cancellationToken);
        if (blocked)
            return Error.Validation("BOOKING_DATA_BLOQUEADA", "Data bloqueada por BlackoutDate (RN-AGD-010).");

        var existingBooking = await db.Bookings.AnyAsync(
            b => b.TenantId == tenantId && b.OrderId == order.Id && b.Status == BookingStatus.Ativo,
            cancellationToken
        );
        if (!existingBooking)
        {
            var start = order.IntendedDate.ToDateTime(TimeOnly.FromTimeSpan(TimeSpan.FromHours(9)), DateTimeKind.Utc);
            var ends = start.AddHours(4);
            var booking = Booking.Create(
                tenantId,
                order.Id,
                new DateTimeOffset(start, TimeSpan.Zero),
                new DateTimeOffset(ends, TimeSpan.Zero),
                travelBufferMinutes: 30,
                orderStatusConfirmado: true
            );
            if (booking.IsFailure)
                return Result.Failure<Unit>(booking.Error!.Value);
            db.Bookings.Add(booking.Value);
        }

        await uow.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed class WebhookPaymentProcessor(PrataDbContext db, IDateTimeProvider clock, ILogger<WebhookPaymentProcessor> logger)
    : IWebhookPaymentProcessor
{
    public async Task ProcessAsync(Guid paymentEventId, CancellationToken cancellationToken = default)
    {
        var paymentEvent = await db.PaymentEvents.FirstOrDefaultAsync(e => e.Id == paymentEventId, cancellationToken);
        if (paymentEvent is null || paymentEvent.ProcessedAt is not null)
            return;

        var chargeId = TryReadChargeId(paymentEvent.PayloadJson) ?? paymentEvent.ExternalEventId;
        // Prefer charge id from payload; Fake envelope stores charge in payload JSON.

        var payment = await db
            .Payments.Include("_installments")
            .FirstOrDefaultAsync(
                p =>
                    p.TenantId == paymentEvent.TenantId
                    && (p.ExternalChargeId == chargeId || p.Installments.Any(i => i.ExternalInstallmentId == chargeId)),
                cancellationToken
            );

        // EF may not translate Installments.Any on field — load then filter.
        if (payment is null)
        {
            var candidates = await db
                .Payments.Include("_installments")
                .Where(p => p.TenantId == paymentEvent.TenantId)
                .ToListAsync(cancellationToken);
            payment = candidates.FirstOrDefault(p =>
                p.ExternalChargeId == chargeId || p.Installments.Any(i => i.ExternalInstallmentId == chargeId)
            );
        }

        if (payment is null)
        {
            paymentEvent.MarcarDescartado("pagamento_nao_encontrado", clock.UtcNow);
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        var installments = payment.Installments;
        Installment? installment = null;
        for (var i = 0; i < installments.Count; i++)
        {
            if (installments[i].ExternalInstallmentId == chargeId)
            {
                installment = installments[i];
                break;
            }
        }

        installment ??= installments.Count > 0 ? installments[0] : null;

        if (installment is null)
        {
            paymentEvent.MarcarDescartado("parcela_ausente", clock.UtcNow);
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        var type = paymentEvent.EventType.ToUpperInvariant();
        Result<Unit> transition;

        if (type is "PAYMENT_RECEIVED" or "PAYMENT_CONFIRMED" or "CONFIRMED")
        {
            if (Rank(installment.Status) >= Rank(InstallmentStatus.Confirmado))
            {
                paymentEvent.MarcarDescartado("evento_atrasado_ou_duplicado", clock.UtcNow);
                await db.SaveChangesAsync(cancellationToken);
                return;
            }

            if (installment.Status == InstallmentStatus.Pendente)
                installment.MarcarProcessando(installment.Method);
            transition = installment.Confirmar(installment.Method, clock.UtcNow);
        }
        else if (type is "PAYMENT_SETTLED" or "SETTLEMENT" or "LIQUIDADO")
        {
            if (Rank(installment.Status) >= Rank(InstallmentStatus.Liquidado))
            {
                paymentEvent.MarcarDescartado("ja_liquidado", clock.UtcNow);
                await db.SaveChangesAsync(cancellationToken);
                return;
            }

            if (installment.Status != InstallmentStatus.Confirmado)
            {
                paymentEvent.MarcarDescartado("liquidacao_fora_de_ordem", clock.UtcNow);
                await db.SaveChangesAsync(cancellationToken);
                return;
            }

            transition = installment.Liquidar(installment.Method, clock.UtcNow);
        }
        else
        {
            paymentEvent.MarcarDescartado($"tipo_nao_tratado:{paymentEvent.EventType}", clock.UtcNow);
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        if (transition.IsFailure)
        {
            paymentEvent.MarcarErro(transition.Error!.Value.Code);
            logger.LogWarning("Webhook falhou type={Type} code={Code}", paymentEvent.EventType, transition.Error!.Value.Code);
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        payment.RecalcularStatus();

        if (installment.Status == InstallmentStatus.Liquidado)
        {
            var account = await db.PayoutAccounts.FirstOrDefaultAsync(a => a.TenantId == payment.TenantId, cancellationToken);
            var kyc = account?.KycStatus ?? KycStatus.Pendente;
            var photographerShare = installment.Amount.Subtract(installment.Amount.Percentage(payment.SplitSnapshot.Percent));
            var payout = Payout.AgendarOuBloquear(payment.TenantId, payment.Id, photographerShare, kyc, clock.UtcNow);
            if (payout.IsSuccess)
                db.Payouts.Add(payout.Value);
        }

        paymentEvent.MarcarProcessado(clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static string? TryReadChargeId(string payloadJson)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(payloadJson);
            if (doc.RootElement.TryGetProperty("chargeId", out var c))
                return c.GetString();
            if (doc.RootElement.TryGetProperty("externalChargeId", out var c2))
                return c2.GetString();
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }

        return null;
    }

    private static int Rank(InstallmentStatus s) =>
        s switch
        {
            InstallmentStatus.Pendente => 0,
            InstallmentStatus.Processando => 1,
            InstallmentStatus.Autorizado => 2,
            InstallmentStatus.Confirmado => 3,
            InstallmentStatus.Liquidado => 4,
            InstallmentStatus.Repassado => 5,
            _ => 99,
        };
}
