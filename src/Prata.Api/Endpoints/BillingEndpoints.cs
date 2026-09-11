using Microsoft.EntityFrameworkCore;
using Prata.Application.Abstractions;
using Prata.Application.Billing;
using Prata.Application.Common;
using Prata.Domain.Billing;
using Prata.Domain.Common;
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
                    IWebhookPaymentProcessor processor,
                    CancellationToken ct
                ) =>
                {
                    using var reader = new StreamReader(request.Body);
                    var raw = await reader.ReadToEndAsync(ct);
                    var headers = request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString(), StringComparer.OrdinalIgnoreCase);

                    var analyzed = gateway.VerificarEAnalisar(raw, headers);
                    if (analyzed.IsFailure)
                    {
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
                    await db.Database.ExecuteSqlRawAsync("SELECT set_config('prata.tenant_id', {0}, true)", tenantId.ToString());

                    var payload =
                        string.IsNullOrWhiteSpace(envelope.PayloadJson) || envelope.PayloadJson == "{}"
                            ? $"{{\"chargeId\":\"{envelope.ExternalChargeId}\"}}"
                            : envelope.PayloadJson;
                    if (!payload.Contains("chargeId", StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(envelope.ExternalChargeId))
                        payload =
                            $"{{\"chargeId\":\"{envelope.ExternalChargeId}\",\"raw\":{System.Text.Json.JsonSerializer.Serialize(envelope.PayloadJson)}}}";

                    var evt = PaymentEvent.Create(tenantId, envelope.ExternalEventId, envelope.EventType, payload, clock.UtcNow);
                    db.PaymentEvents.Add(evt);
                    await db.SaveChangesAsync(ct);

                    await processor.ProcessAsync(evt.Id, ct);
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

                var payments = await db.Payments.AsNoTracking().Where(p => p.TenantId == tenantId).ToListAsync(ct);

                decimal AReceber() =>
                    payments
                        .Where(p => p.Status is PaymentStatus.Pendente or PaymentStatus.ParcialmentePago)
                        .Sum(p => p.Total.Amount - p.PlatformFeeAmount.Amount);

                decimal Liquidado() =>
                    payments
                        .Where(p => p.Status is PaymentStatus.Confirmado or PaymentStatus.Liquidado)
                        .Sum(p => p.Total.Amount - p.PlatformFeeAmount.Amount);

                var payouts = await db.Payouts.AsNoTracking().Where(p => p.TenantId == tenantId).ToListAsync(ct);
                var repassado = payouts.Where(p => p.Status == PayoutStatus.Liquidado).Sum(p => p.Amount.Amount);
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

                var account = await db.PayoutAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.TenantId == tenantId, ct);
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

        studio.MapPost(
            "/payout-account",
            async (
                OnboardPayoutBody body,
                ITenantContext tenant,
                IPaymentGateway gateway,
                PrataDbContext db,
                IDateTimeProvider clock,
                CancellationToken ct
            ) =>
            {
                if (tenant.TenantId is not Guid tenantId)
                    return Results.BadRequest(new { type = "TENANT_AUSENTE" });

                var existing = await db.PayoutAccounts.FirstOrDefaultAsync(a => a.TenantId == tenantId, ct);
                if (existing is not null)
                    return Results.Conflict(new { type = "PAYOUT_ACCOUNT_JA_EXISTE" });

                var created = await gateway.CriarRecebedorAsync(
                    new RecebedorRequest(tenantId, body.Name, body.DocumentMasked, body.PixKeyMasked, body.Email),
                    ct
                );
                if (created.IsFailure)
                    return Results.BadRequest(new { type = created.Error!.Value.Code, title = created.Error.Value.Message });

                var account = PayoutAccount.Create(
                    tenantId,
                    created.Value.ExternalRecipientId,
                    body.DocumentMasked,
                    body.PixKeyMasked,
                    body.BankMasked,
                    clock.UtcNow
                );
                if (account.IsFailure)
                    return Results.BadRequest(new { type = account.Error!.Value.Code, title = account.Error.Value.Message });

                db.PayoutAccounts.Add(account.Value);
                await db.SaveChangesAsync(ct);
                return Results.Created(
                    "/v1/studio/finance/payout-account",
                    new { id = account.Value.Id, account.Value.ExternalRecipientId }
                );
            }
        );

        studio.MapPost(
            "/payout-account/kyc",
            async (KycBody body, ITenantContext tenant, PrataDbContext db, IDateTimeProvider clock, CancellationToken ct) =>
            {
                if (tenant.TenantId is not Guid tenantId)
                    return Results.BadRequest(new { type = "TENANT_AUSENTE" });

                var account = await db.PayoutAccounts.FirstOrDefaultAsync(a => a.TenantId == tenantId, ct);
                if (account is null)
                    return Results.NotFound();

                if (!Enum.TryParse<KycStatus>(body.Status, true, out var status))
                    return Results.BadRequest(new { type = "KYC_STATUS_INVALIDO" });

                account.AtualizarKyc(status, clock.UtcNow);

                if (status == KycStatus.Aprovado)
                {
                    var bloqueados = await db
                        .Payouts.Where(p => p.TenantId == tenantId && p.Status == PayoutStatus.BloqueadoKyc)
                        .ToListAsync(ct);
                    foreach (var p in bloqueados)
                        p.LiberarAposKyc(clock.UtcNow);
                }

                await db.SaveChangesAsync(ct);
                return Results.NoContent();
            }
        );

        app.MapPost(
                "/v1/portal/orders/{id:guid}/confirm",
                async (Guid id, ConfirmBody? body, IDispatcher dispatcher, CancellationToken ct) =>
                {
                    var result = await dispatcher.Send(new ConfirmOrderCommand(id, body?.ContratoAssinado ?? false), ct);
                    return result.IsSuccess
                        ? Results.NoContent()
                        : Results.Json(
                            new { type = result.Error!.Value.Code, title = result.Error.Value.Message },
                            statusCode: StatusCodes.Status400BadRequest
                        );
                }
            )
            .WithTags("Portal")
            .RequireRateLimiting("tenant");

        return app;
    }
}

public sealed record OnboardPayoutBody(string Name, string DocumentMasked, string? PixKeyMasked, string? BankMasked, string? Email);

public sealed record KycBody(string Status);

public sealed record ConfirmBody(bool ContratoAssinado);
