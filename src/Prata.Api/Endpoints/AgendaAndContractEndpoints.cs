using Microsoft.EntityFrameworkCore;
using Prata.Application.Abstractions;
using Prata.Domain.Common;
using Prata.Domain.Contracts;
using Prata.Domain.Tenancy;
using Prata.Infrastructure.Contracts;
using Prata.Infrastructure.Persistence;

namespace Prata.Api.Endpoints;

public static class AgendaAndContractEndpoints
{
    public static IEndpointRouteBuilder MapAgendaAndContractEndpoints(this IEndpointRouteBuilder app)
    {
        var studio = app.MapGroup("/v1/studio").WithTags("AgendaContrato").RequireAuthorization();

        studio.MapPost(
            "/availability",
            async (
                AvailabilityRequest body,
                ITenantContext tenant,
                ISchedulingService scheduling,
                CancellationToken ct
            ) =>
            {
                if (!tenant.IsResolved)
                    return Results.Unauthorized();
                var result = await scheduling.UpsertAvailabilityAsync(
                    tenant.TenantId!.Value,
                    body.Weekday,
                    body.StartsAt,
                    body.EndsAt,
                    ct
                );
                return result.IsFailure ? Results.BadRequest(Problem(result.Error!.Value)) : Results.NoContent();
            }
        );

        studio.MapPost(
            "/blackouts",
            async (BlackoutRequest body, ITenantContext tenant, ISchedulingService scheduling, CancellationToken ct) =>
            {
                if (!tenant.IsResolved)
                    return Results.Unauthorized();
                var result = await scheduling.AddBlackoutAsync(tenant.TenantId!.Value, body.Date, body.Reason, ct);
                return result.IsFailure ? Results.BadRequest(Problem(result.Error!.Value)) : Results.NoContent();
            }
        );

        studio.MapPost(
            "/orders/{orderId:guid}/schedule",
            async (
                Guid orderId,
                ScheduleRequest body,
                ITenantContext tenant,
                ISchedulingService scheduling,
                CancellationToken ct
            ) =>
            {
                if (!tenant.IsResolved)
                    return Results.Unauthorized();
                var result = await scheduling.AgendarPedidoAsync(
                    tenant.TenantId!.Value,
                    orderId,
                    body.StartsAt,
                    body.EndsAt,
                    body.TravelBufferMinutes,
                    ct
                );
                return result.IsFailure ? Results.BadRequest(Problem(result.Error!.Value)) : Results.NoContent();
            }
        );

        studio.MapPost(
            "/orders/{orderId:guid}/contracts",
            async (Guid orderId, ContractCreateRequest body, ITenantContext tenant, IContractService contracts, CancellationToken ct) =>
            {
                if (!tenant.IsResolved)
                    return Results.Unauthorized();
                var result = await contracts.CriarEEnviarAsync(
                    tenant.TenantId!.Value,
                    orderId,
                    body.TemplateVersion ?? "v1",
                    body.ValidadeDias <= 0 ? 7 : body.ValidadeDias,
                    ct
                );
                return result.IsFailure
                    ? Results.BadRequest(Problem(result.Error!.Value))
                    : Results.Created($"/v1/studio/contracts/{result.Value}", new { id = result.Value });
            }
        );

        studio.MapPost(
            "/contracts/{contractId:guid}/corrections",
            async (
                Guid contractId,
                ContractCreateRequest body,
                ITenantContext tenant,
                IContractService contracts,
                CancellationToken ct
            ) =>
            {
                if (!tenant.IsResolved)
                    return Results.Unauthorized();
                var result = await contracts.CorrigirAsync(
                    tenant.TenantId!.Value,
                    contractId,
                    body.TemplateVersion ?? "v1-fix",
                    body.ValidadeDias <= 0 ? 7 : body.ValidadeDias,
                    ct
                );
                return result.IsFailure
                    ? Results.BadRequest(Problem(result.Error!.Value))
                    : Results.Created($"/v1/studio/contracts/{result.Value}", new { id = result.Value });
            }
        );

        studio.MapGet(
            "/pendencies/signatures",
            async (ITenantContext tenant, PrataDbContext db, CancellationToken ct) =>
            {
                if (!tenant.IsResolved)
                    return Results.Unauthorized();
                var tenantId = tenant.TenantId!.Value;
                var deposits = await db
                    .Payments.AsNoTracking()
                    .Where(p => p.TenantId == tenantId && p.Kind == Domain.Billing.PaymentKind.Deposit)
                    .ToListAsync(ct);
                var pending = new List<object>();
                foreach (var p in deposits.Where(x => x.SinalConfirmado))
                {
                    var signed = await db.Contracts.AnyAsync(
                        c => c.TenantId == tenantId && c.OrderId == p.OrderId && c.Status == ContractStatus.Assinado,
                        ct
                    );
                    if (!signed)
                        pending.Add(new { orderId = p.OrderId, paymentId = p.Id });
                }

                return Results.Ok(pending);
            }
        );

        studio.MapPut(
            "/domain",
            async (CustomDomainRequest body, ITenantContext tenant, PrataDbContext db, IUnitOfWork uow, CancellationToken ct) =>
            {
                if (!tenant.IsResolved)
                    return Results.Unauthorized();
                var entity = await db.Tenants.FirstOrDefaultAsync(t => t.Id == tenant.TenantId, ct);
                if (entity is null)
                    return Results.NotFound();
                var result = entity.SolicitarDominioProprio(body.Domain);
                if (result.IsFailure)
                    return Results.BadRequest(Problem(result.Error!.Value));
                await uow.SaveChangesAsync(ct);
                return Results.Ok(
                    new
                    {
                        domain = entity.CustomDomain,
                        status = entity.CustomDomainStatus.ToString(),
                        cnameTarget = $"{entity.Slug.Value}.prata.app",
                        instruction = "Aponte o CNAME do dominio para o subdominio. SSL e emitido apos verificacao (RN-TEN-013).",
                    }
                );
            }
        );

        app.MapPost(
                "/v1/portal/contracts/{contractId:guid}/sign",
                async (
                    Guid contractId,
                    SignContractRequest body,
                    ITenantContext tenant,
                    IContractService contracts,
                    HttpRequest request,
                    CancellationToken ct
                ) =>
                {
                    if (!tenant.IsResolved)
                        return Results.Unauthorized();
                    var ip = request.HttpContext.Connection.RemoteIpAddress?.ToString() ?? body.Ip ?? "";
                    var ua = request.Headers.UserAgent.ToString();
                    if (string.IsNullOrWhiteSpace(ua))
                        ua = body.UserAgent ?? "";
                    var result = await contracts.AssinarAsync(
                        tenant.TenantId!.Value,
                        contractId,
                        body.SignerName,
                        body.SignerEmail,
                        ip,
                        ua,
                        ct
                    );
                    return result.IsFailure ? Results.BadRequest(Problem(result.Error!.Value)) : Results.NoContent();
                }
            )
            .RequireAuthorization()
            .WithTags("AgendaContrato");

        app.MapGet(
                "/v1/redirect-check",
                async (HttpRequest request, PrataDbContext db, CancellationToken ct) =>
                {
                    // Diagnostico: se host e subdominio e tenant tem dominio ativo, indica 301 (RN-TEN-013).
                    var host = request.Host.Host.ToLowerInvariant();
                    if (!host.EndsWith(".prata.app", StringComparison.Ordinal))
                        return Results.Ok(new { redirect = false });

                    var slug = host[..^".prata.app".Length];
                    var tenant = await db
                        .Tenants.AsNoTracking()
                        .FirstOrDefaultAsync(t => t.Slug.Value == slug, ct);
                    if (
                        tenant is null
                        || tenant.CustomDomainStatus != CustomDomainStatus.Ativo
                        || string.IsNullOrWhiteSpace(tenant.CustomDomain)
                    )
                        return Results.Ok(new { redirect = false });

                    var path = request.Path + request.QueryString;
                    return Results.Ok(
                        new
                        {
                            redirect = true,
                            location = $"https://{tenant.CustomDomain}{path}",
                            status = 301,
                        }
                    );
                }
            )
            .AllowAnonymous()
            .WithTags("AgendaContrato");

        return app;
    }

    private static object Problem(Error error) => new { type = error.Code, title = error.Message };

    public sealed record AvailabilityRequest(DayOfWeek Weekday, TimeOnly StartsAt, TimeOnly EndsAt);

    public sealed record BlackoutRequest(DateOnly Date, string Reason);

    public sealed record ScheduleRequest(DateTimeOffset StartsAt, DateTimeOffset EndsAt, int TravelBufferMinutes);

    public sealed record ContractCreateRequest(string? TemplateVersion, int ValidadeDias);

    public sealed record SignContractRequest(string SignerName, string SignerEmail, string? Ip, string? UserAgent);

    public sealed record CustomDomainRequest(string Domain);
}
