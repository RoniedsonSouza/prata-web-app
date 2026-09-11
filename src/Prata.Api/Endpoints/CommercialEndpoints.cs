using System.Data;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Prata.Application.Abstractions;
using Prata.Application.Common;
using Prata.Application.Sales;
using Prata.Domain.Common;
using Prata.Domain.Sales;
using Prata.Infrastructure;
using Prata.Infrastructure.Persistence;

namespace Prata.Api.Endpoints;

public static class CommercialEndpoints
{
    public static IEndpointRouteBuilder MapCommercialEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/v1/portal/orders",
                async (CreateOrderRequest body, IDispatcher dispatcher, CancellationToken ct) =>
                {
                    var channel = Enum.TryParse<PreferredChannel>(body.PreferredChannel, true, out var parsed)
                        ? parsed
                        : PreferredChannel.Email;

                    var result = await dispatcher.Send(
                        new CreateOrderCommand(
                            body.ClientName,
                            body.ClientEmail,
                            body.ClientWhatsApp,
                            channel,
                            body.ServiceTypeId,
                            body.IntendedDate,
                            body.PackageId
                        ),
                        ct
                    );

                    return result.IsSuccess
                        ? Results.Created($"/v1/portal/orders/{result.Value}", new { id = result.Value })
                        : Problem(result.Error!.Value);
                }
            )
            .WithTags("Portal")
            .RequireRateLimiting("tenant");

        app.MapPost(
                "/v1/portal/orders/{id:guid}/submit",
                async (Guid id, IDispatcher dispatcher, CancellationToken ct) =>
                {
                    var result = await dispatcher.Send(new SubmitOrderCommand(id), ct);
                    return result.IsSuccess ? Results.NoContent() : Problem(result.Error!.Value);
                }
            )
            .WithTags("Portal")
            .RequireRateLimiting("tenant");

        app.MapPost(
                "/v1/portal/orders/{id:guid}/approve-quote",
                async (Guid id, IDispatcher dispatcher, CancellationToken ct) =>
                {
                    var result = await dispatcher.Send(new ApproveQuoteCommand(id), ct);
                    return result.IsSuccess ? Results.NoContent() : Problem(result.Error!.Value);
                }
            )
            .WithTags("Portal")
            .RequireRateLimiting("tenant");

        var studio = app.MapGroup("/v1/studio/orders")
            .WithTags("StudioOrders")
            .RequireAuthorization();

        studio.MapGet(
            "/",
            async (
                string? status,
                ITenantContext tenant,
                IConfiguration config,
                CancellationToken ct
            ) =>
            {
                if (tenant.TenantId is not Guid tenantId)
                    return Results.BadRequest(new { type = "TENANT_AUSENTE", title = "Tenant nao resolvido." });

                var cs =
                    config.GetConnectionString(DependencyInjection.AppConnectionName)
                    ?? config.GetConnectionString("Prata")
                    ?? throw new InvalidOperationException("Connection string ausente.");

                await using var conn = new NpgsqlConnection(cs);
                await conn.OpenAsync(ct);
                await using var guc = conn.CreateCommand();
                guc.CommandText = "SELECT set_config('prata.tenant_id', @tenantId, true)";
                guc.Parameters.AddWithValue("tenantId", tenantId.ToString());
                await guc.ExecuteNonQueryAsync(ct);

                // Dapper com TenantId explicito (checklist multi-tenancy).
                const string sql = """
                    SELECT id, status, intended_date AS IntendedDate, total_amount AS Total, created_at AS CreatedAt
                    FROM "order"
                    WHERE tenant_id = @TenantId
                      AND (@Status IS NULL OR status = @Status)
                    ORDER BY created_at DESC
                    LIMIT 100
                    """;

                var rows = await conn.QueryAsync<OrderListRow>(
                    new CommandDefinition(
                        sql,
                        new { TenantId = tenantId, Status = string.IsNullOrWhiteSpace(status) ? null : status },
                        cancellationToken: ct
                    )
                );

                return Results.Ok(rows);
            }
        );

        studio.MapGet(
            "/{id:guid}",
            async (Guid id, ITenantContext tenant, PrataDbContext db, CancellationToken ct) =>
            {
                if (tenant.TenantId is not Guid tenantId)
                    return Results.BadRequest(new { type = "TENANT_AUSENTE", title = "Tenant nao resolvido." });

                var order = await db
                    .Orders.AsNoTracking()
                    .Include("_items")
                    .Include("_quotes")
                    .FirstOrDefaultAsync(o => o.TenantId == tenantId && o.Id == id, ct);

                if (order is null)
                    return Results.NotFound();

                return Results.Ok(
                    new
                    {
                        order.Id,
                        Status = order.Status.ToString(),
                        order.ClientId,
                        order.ServiceTypeId,
                        order.IntendedDate,
                        Subtotal = order.Subtotal.Amount,
                        Discount = order.DiscountAmount.Amount,
                        DiscountKind = order.Discount?.Kind.ToString(),
                        DiscountPercent = order.Discount?.Percent,
                        DiscountFixed = order.Discount?.FixedAmount?.Amount,
                        Total = order.Total.Amount,
                        SensitiveStaffUserIds = order.SensitiveStaffUserIds,
                        Items = order.Items.Select(i => new
                        {
                            i.Id,
                            Kind = i.Kind.ToString(),
                            i.NameSnapshot,
                            UnitPrice = i.UnitPriceSnapshot.Amount,
                            i.Quantity,
                            LineTotal = i.LineTotal.Amount,
                        }),
                        Quote = order.CurrentQuote is null
                            ? null
                            : new
                            {
                                order.CurrentQuote.Version,
                                order.CurrentQuote.ValidoAte,
                                Total = order.CurrentQuote.TotalSnapshot.Amount,
                            },
                    }
                );
            }
        );

        studio.MapPost(
            "/{id:guid}/analyze",
            async (Guid id, IDispatcher dispatcher, CancellationToken ct) =>
            {
                var result = await dispatcher.Send(new AnalyzeOrderCommand(id), ct);
                return result.IsSuccess ? Results.NoContent() : Problem(result.Error!.Value);
            }
        );

        studio.MapPost(
            "/{id:guid}/refuse",
            async (Guid id, RefuseBody body, IDispatcher dispatcher, CancellationToken ct) =>
            {
                var result = await dispatcher.Send(new RefuseOrderCommand(id, body.Motivo), ct);
                return result.IsSuccess ? Results.NoContent() : Problem(result.Error!.Value);
            }
        );

        studio.MapPost(
            "/{id:guid}/send-quote",
            async (Guid id, SendQuoteBody? body, IDispatcher dispatcher, CancellationToken ct) =>
            {
                var result = await dispatcher.Send(new SendQuoteCommand(id, body?.ValidadeDias), ct);
                return result.IsSuccess ? Results.NoContent() : Problem(result.Error!.Value);
            }
        );

        studio.MapPost(
            "/{id:guid}/hold",
            async (Guid id, HoldBody body, IDispatcher dispatcher, CancellationToken ct) =>
            {
                var result = await dispatcher.Send(new PutOrderOnHoldCommand(id, body.Motivo), ct);
                return result.IsSuccess ? Results.NoContent() : Problem(result.Error!.Value);
            }
        );

        studio.MapPost(
            "/{id:guid}/resume",
            async (Guid id, IDispatcher dispatcher, CancellationToken ct) =>
            {
                var result = await dispatcher.Send(new ResumeOrderCommand(id), ct);
                return result.IsSuccess ? Results.NoContent() : Problem(result.Error!.Value);
            }
        );

        studio.MapPost(
            "/{id:guid}/items/package",
            async (Guid id, AddPackageBody body, IDispatcher dispatcher, CancellationToken ct) =>
            {
                var result = await dispatcher.Send(new AddOrderPackageCommand(id, body.PackageId), ct);
                return result.IsSuccess ? Results.NoContent() : Problem(result.Error!.Value);
            }
        );

        studio.MapPost(
            "/{id:guid}/items/addon",
            async (Guid id, AddAddonBody body, IDispatcher dispatcher, CancellationToken ct) =>
            {
                var result = await dispatcher.Send(
                    new AddOrderAddonCommand(id, body.AddonId, body.Quantity ?? 1),
                    ct
                );
                return result.IsSuccess ? Results.NoContent() : Problem(result.Error!.Value);
            }
        );

        studio.MapDelete(
            "/{id:guid}/items/{itemId:guid}",
            async (Guid id, Guid itemId, IDispatcher dispatcher, CancellationToken ct) =>
            {
                var result = await dispatcher.Send(new RemoveOrderItemCommand(id, itemId), ct);
                return result.IsSuccess ? Results.NoContent() : Problem(result.Error!.Value);
            }
        );

        studio.MapPost(
            "/{id:guid}/discount",
            async (Guid id, DiscountBody body, IDispatcher dispatcher, CancellationToken ct) =>
            {
                var result = await dispatcher.Send(
                    new ApplyOrderDiscountCommand(id, body.Kind, body.FixedAmount, body.Percent),
                    ct
                );
                return result.IsSuccess ? Results.NoContent() : Problem(result.Error!.Value);
            }
        );

        studio.MapDelete(
            "/{id:guid}/discount",
            async (Guid id, IDispatcher dispatcher, CancellationToken ct) =>
            {
                var result = await dispatcher.Send(new ClearOrderDiscountCommand(id), ct);
                return result.IsSuccess ? Results.NoContent() : Problem(result.Error!.Value);
            }
        );

        studio.MapGet(
            "/{id:guid}/catalog-options",
            async (Guid id, ITenantContext tenant, IOrderRepository orders, ICatalogReader catalog, CancellationToken ct) =>
            {
                if (tenant.TenantId is not Guid tenantId)
                    return Results.BadRequest(new { type = "TENANT_AUSENTE", title = "Tenant nao resolvido." });

                var order = await orders.GetByIdAsync(tenantId, id, ct);
                if (order is null)
                    return Results.NotFound();

                var packages = await catalog.ListPublishedPackagesAsync(tenantId, order.ServiceTypeId, ct);
                var addons = await catalog.ListActiveAddonsAsync(tenantId, ct);
                return Results.Ok(
                    new
                    {
                        packages = packages.Select(p => new
                        {
                            p.Id,
                            p.Name,
                            Price = p.Price?.Amount,
                        }),
                        addons = addons.Select(a => new
                        {
                            a.Id,
                            a.Name,
                            Price = a.Price.Amount,
                        }),
                    }
                );
            }
        );

        studio.MapPost(
            "/{id:guid}/sensitive-staff",
            async (Guid id, SensitiveStaffBody body, IDispatcher dispatcher, CancellationToken ct) =>
            {
                var result = await dispatcher.Send(new DesignateSensitiveStaffCommand(id, body.UserId), ct);
                return result.IsSuccess ? Results.NoContent() : Problem(result.Error!.Value);
            }
        );

        studio.MapDelete(
            "/{id:guid}/sensitive-staff/{userId:guid}",
            async (Guid id, Guid userId, IDispatcher dispatcher, CancellationToken ct) =>
            {
                var result = await dispatcher.Send(new RemoveSensitiveStaffCommand(id, userId), ct);
                return result.IsSuccess ? Results.NoContent() : Problem(result.Error!.Value);
            }
        );

        return app;
    }

    private static IResult Problem(Error error) =>
        Results.Json(
            new { type = error.Code, title = error.Message },
            statusCode: StatusCodes.Status400BadRequest
        );
}

public sealed record CreateOrderRequest(
    string ClientName,
    string ClientEmail,
    string? ClientWhatsApp,
    string PreferredChannel,
    Guid ServiceTypeId,
    DateOnly IntendedDate,
    Guid? PackageId
);

public sealed record RefuseBody(string Motivo);

public sealed record SendQuoteBody(int? ValidadeDias);

public sealed record HoldBody(string Motivo);

public sealed record SensitiveStaffBody(Guid UserId);

public sealed record AddPackageBody(Guid PackageId);

public sealed record AddAddonBody(Guid AddonId, int? Quantity);

public sealed record DiscountBody(string Kind, decimal? FixedAmount, decimal? Percent);

internal sealed class OrderListRow
{
    public Guid Id { get; init; }

    public string Status { get; init; } = string.Empty;

    public DateOnly IntendedDate { get; init; }

    public decimal Total { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}
