using Microsoft.EntityFrameworkCore;
using Prata.Application.Abstractions;
using Prata.Domain.Billing;
using Prata.Domain.Tenancy;
using Prata.Infrastructure.Persistence;

namespace Prata.Api.Endpoints;

public static class BillingEndpoints
{
    public static IEndpointRouteBuilder MapBillingEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/v1/webhooks/asaas",
                async (
                    HttpRequest request,
                    IPaymentGateway gateway,
                    PrataDbContext db,
                    MutableTenantContext tenantContext,
                    IDateTimeProvider clock,
                    CancellationToken ct
                ) =>
                {
                    using var reader = new StreamReader(request.Body);
                    var raw = await reader.ReadToEndAsync(ct);
                    var headers = request.Headers.ToDictionary(
                        h => h.Key,
                        h => h.Value.ToString(),
                        StringComparer.OrdinalIgnoreCase
                    );

                    var analyzed = gateway.VerificarEAnalisar(raw, headers);
                    if (analyzed.IsFailure)
                    {
                        // RN-FIN-020 — assinatura invalida: 401 e nada gravado.
                        return Results.Json(
                            new { type = analyzed.Error!.Value.Code, title = analyzed.Error.Value.Message },
                            statusCode: StatusCodes.Status401Unauthorized
                        );
                    }

                    var envelope = analyzed.Value;
                    var exists = await db
                        .PaymentEvents.IgnoreQueryFilters()
                        .AnyAsync(e => e.ExternalEventId == envelope.ExternalEventId, ct);
                    if (exists)
                        return Results.Ok(new { duplicate = true });

                    var tenantId = envelope.TenantId ?? Guid.Empty;
                    if (tenantId == Guid.Empty)
                        return Results.BadRequest(new { type = "TENANT_AUSENTE" });

                    tenantContext.Set(tenantId, "webhook-asaas", TenantStatus.Ativo);
                    await db.Database.OpenConnectionAsync(ct);
                    await db.Database.ExecuteSqlRawAsync(
                        "SELECT set_config('prata.tenant_id', {0}, true)",
                        tenantId.ToString()
                    );

                    db.PaymentEvents.Add(
                        PaymentEvent.Create(
                            tenantId,
                            envelope.ExternalEventId,
                            envelope.EventType,
                            envelope.PayloadJson,
                            clock.UtcNow
                        )
                    );
                    await db.SaveChangesAsync(ct);
                    return Results.Ok(new { accepted = true });
                }
            )
            .AllowAnonymous()
            .WithTags("Webhooks");

        var studio = app.MapGroup("/v1/studio/finance").WithTags("StudioFinance").RequireAuthorization();

        studio.MapGet(
            "/summary",
            async (ITenantContext tenant, PrataDbContext db, CancellationToken ct) =>
            {
                if (tenant.TenantId is not Guid tenantId)
                    return Results.BadRequest(new { type = "TENANT_AUSENTE" });

                var payments = await db
                    .Payments.AsNoTracking()
                    .Where(p => p.TenantId == tenantId)
                    .ToListAsync(ct);

                // Tres colunas: a receber, liquidado, repassado (aceite E3).
                decimal AReceber() =>
                    payments
                        .Where(p => p.Status is PaymentStatus.Pendente or PaymentStatus.ParcialmentePago)
                        .Sum(p => p.Total.Amount - p.PlatformFeeAmount.Amount);

                decimal Liquidado() =>
                    payments
                        .Where(p => p.Status is PaymentStatus.Confirmado or PaymentStatus.Liquidado)
                        .Sum(p => p.Total.Amount - p.PlatformFeeAmount.Amount);

                var payouts = await db
                    .Payouts.AsNoTracking()
                    .Where(p => p.TenantId == tenantId)
                    .ToListAsync(ct);

                var repassado = payouts
                    .Where(p => p.Status == PayoutStatus.Liquidado)
                    .Sum(p => p.Amount.Amount);
                var bloqueadoKyc = payouts.Any(p => p.Status == PayoutStatus.BloqueadoKyc);

                return Results.Ok(
                    new
                    {
                        aReceber = AReceber(),
                        liquidado = Liquidado(),
                        repassado,
                        kycBloqueandoRepasse = bloqueadoKyc,
                    }
                );
            }
        );

        studio.MapGet(
            "/payout-account",
            async (ITenantContext tenant, PrataDbContext db, CancellationToken ct) =>
            {
                if (tenant.TenantId is not Guid tenantId)
                    return Results.BadRequest(new { type = "TENANT_AUSENTE" });

                var account = await db
                    .PayoutAccounts.AsNoTracking()
                    .FirstOrDefaultAsync(a => a.TenantId == tenantId, ct);
                if (account is null)
                    return Results.NotFound();

                return Results.Ok(
                    new
                    {
                        account.ExternalRecipientId,
                        KycStatus = account.KycStatus.ToString(),
                        account.HolderDocumentMasked,
                        account.PixKeyMasked,
                        account.KycUpdatedAt,
                    }
                );
            }
        );

        return app;
    }
}
