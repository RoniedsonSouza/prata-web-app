using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Prata.Application.Abstractions;
using Prata.Application.Briefing;
using Prata.Application.Common;
using Prata.Domain.Briefing;
using Prata.Domain.Common;
using Prata.Domain.Tenancy;
using Prata.Infrastructure.Briefing;
using Prata.Infrastructure.Notifications;
using Prata.Infrastructure.Persistence;
using Prata.Infrastructure.Storage;

namespace Prata.Api.Endpoints;

public static class BriefingEndpoints
{
    public static IEndpointRouteBuilder MapBriefingEndpoints(this IEndpointRouteBuilder app)
    {
        var portal = app.MapGroup("/v1/portal/orders/{orderId:guid}/briefing")
            .WithTags("Briefing")
            .RequireRateLimiting("tenant");

        portal.MapGet(
            "/",
            async (
                Guid orderId,
                ITenantContext tenant,
                IOrderRepository orders,
                IBriefingTemplateRepository templates,
                IAnswerRepository answers,
                IBriefingConsentRepository consents,
                CancellationToken ct
            ) =>
            {
                if (tenant.TenantId is not Guid tenantId)
                    return Results.BadRequest(new { type = "TENANT_AUSENTE" });

                var order = await orders.GetByIdAsync(tenantId, orderId, ct);
                if (order is null)
                    return Results.NotFound();

                var template = await templates.GetPublishedForServiceAsync(tenantId, order.ServiceTypeId, ct);
                var existing = await answers.ListByOrderAsync(tenantId, orderId, ct);
                var byCode = existing.ToDictionary(a => a.QuestionCode, StringComparer.Ordinal);
                var consent = await consents.GetActiveAsync(tenantId, orderId, ct);

                var questions = template
                    ?.Questions.OrderBy(q => q.SortOrder)
                    .Select(q =>
                    {
                        var visible = VisibleWhenEvaluator.IsVisible(q, byCode);
                        var answer = existing.FirstOrDefault(a => a.QuestionCode == q.Code)?.ValueJson;
                        return new
                        {
                            q.Code,
                            q.Prompt,
                            Type = q.Type.ToString(),
                            Block = q.Block.ToString(),
                            q.IsRequired,
                            q.IsSensitive,
                            q.VisibleWhen,
                            Visible = visible,
                            SuggestSelfieTest = BriefingUxHints.SuggestSelfieSideTest(q.Code, answer),
                            Options = q.Options.Select(o => new
                            {
                                o.Code,
                                o.Label,
                                o.IsSuggestedChip,
                            }),
                            Answer = answer,
                        };
                    });

                var visibleQs = template?.Questions.Where(q => VisibleWhenEvaluator.IsVisible(q, byCode)).ToList() ?? [];
                var answeredVisible = visibleQs.Count(q => byCode.ContainsKey(q.Code));

                return Results.Ok(
                    new
                    {
                        orderId,
                        consentActive = consent is not null,
                        purposeText = BriefingPurpose.DefaultText,
                        progress = new
                        {
                            total = visibleQs.Count,
                            answered = answeredVisible,
                            byBlock = visibleQs
                                .GroupBy(q => q.Block.ToString())
                                .ToDictionary(
                                    g => g.Key,
                                    g => new
                                    {
                                        total = g.Count(),
                                        answered = g.Count(q => byCode.ContainsKey(q.Code)),
                                    }
                                ),
                        },
                        questions,
                    }
                );
            }
        );

        portal.MapPut(
            "/answers/{code}",
            async (Guid orderId, string code, AutosaveBody body, IDispatcher dispatcher, CancellationToken ct) =>
            {
                var result = await dispatcher.Send(new AutosaveAnswerCommand(orderId, code, body.ValueJson), ct);
                return result.IsSuccess
                    ? Results.NoContent()
                    : Results.Json(
                        new { type = result.Error!.Value.Code, title = result.Error.Value.Message },
                        statusCode: StatusCodes.Status400BadRequest
                    );
            }
        );

        portal.MapPost(
            "/consent",
            async (Guid orderId, HttpRequest request, IDispatcher dispatcher, CancellationToken ct) =>
            {
                var ip = request.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0";
                var ua = request.Headers.UserAgent.ToString();
                var result = await dispatcher.Send(
                    new RegisterBriefingConsentCommand(orderId, ip, ua, BriefingPurpose.DefaultText),
                    ct
                );
                return result.IsSuccess ? Results.NoContent() : Problem(result.Error!.Value);
            }
        );

        portal.MapPost(
            "/consent/revoke",
            async (Guid orderId, IDispatcher dispatcher, CancellationToken ct) =>
            {
                var result = await dispatcher.Send(new RevokeBriefingConsentCommand(orderId), ct);
                return result.IsSuccess ? Results.NoContent() : Problem(result.Error!.Value);
            }
        );

        portal.MapPost(
            "/uploads",
            async (
                Guid orderId,
                UploadRequest body,
                ITenantContext tenant,
                IPortfolioUploadService uploads,
                CancellationToken ct
            ) =>
            {
                if (tenant.TenantId is not Guid tenantId)
                    return Results.BadRequest(new { type = "TENANT_AUSENTE" });

                var contentType = string.IsNullOrWhiteSpace(body.ContentType) ? "image/jpeg" : body.ContentType;
                var signed = await uploads.CreateSignedUploadAsync(
                    tenantId,
                    $"briefing-{orderId:N}.jpg",
                    contentType,
                    ct
                );
                return Results.Ok(
                    new
                    {
                        objectKey = signed.ObjectKey,
                        uploadUrl = signed.UploadUrl,
                        expiresAt = signed.ExpiresAt,
                    }
                );
            }
        );

        var studio = app.MapGroup("/v1/studio/orders/{orderId:guid}/briefing")
            .WithTags("StudioBriefing")
            .RequireAuthorization();

        studio.MapGet(
            "/",
            async (
                Guid orderId,
                ClaimsPrincipal user,
                ITenantContext tenant,
                IOrderRepository orders,
                IAnswerRepository answers,
                PrataDbContext db,
                CancellationToken ct
            ) =>
            {
                if (tenant.TenantId is not Guid tenantId)
                    return Results.BadRequest(new { type = "TENANT_AUSENTE" });

                var order = await orders.GetByIdAsync(tenantId, orderId, ct);
                if (order is null)
                    return Results.NotFound();

                var userId = Guid.TryParse(user.FindFirstValue("sub"), out var uid) ? uid : Guid.Empty;
                var isOwner = user.IsInRole(TenantRoles.Owner) || user.HasClaim("role", TenantRoles.Owner);
                var canSeeSensitive = order.PodeVerBriefingSensivel(userId, isOwner);

                var list = await answers.ListByOrderAsync(tenantId, orderId, ct);
                var payload = new List<object>();
                foreach (var a in list)
                {
                    if (a.IsSensitive && !canSeeSensitive)
                    {
                        payload.Add(
                            new
                            {
                                a.QuestionCode,
                                a.PromptSnapshot,
                                sensitive = true,
                                redacted = true,
                            }
                        );
                        continue;
                    }

                    if (a.IsSensitive)
                    {
                        db.AuditLogs.Add(
                            new AuditLog
                            {
                                Id = Guid.NewGuid(),
                                TenantId = tenantId,
                                UserId = userId == Guid.Empty ? null : userId,
                                Action = "LerBriefingSensivel",
                                EntityType = "Answer",
                                EntityId = a.Id,
                                At = DateTimeOffset.UtcNow,
                                Metadata = "{\"orderId\":\"" + orderId + "\",\"code\":\"" + a.QuestionCode + "\"}",
                            }
                        );
                    }

                    payload.Add(
                        new
                        {
                            a.QuestionCode,
                            a.PromptSnapshot,
                            Type = a.TypeSnapshot.ToString(),
                            a.IsSensitive,
                            value = a.ValueJson,
                        }
                    );
                }

                if (canSeeSensitive)
                    await db.SaveChangesAsync(ct);

                return Results.Ok(payload);
            }
        );

        // Emite URL assinada — PDF so via GET /v1/fichas/{token} (RN-BRF-040).
        studio.MapPost(
            "/direction-sheet-link",
            (
                Guid orderId,
                ClaimsPrincipal user,
                ITenantContext tenant,
                ISignedFichaService signedFicha,
                IConfiguration config
            ) =>
            {
                if (tenant.TenantId is not Guid tenantId)
                    return Results.BadRequest(new { type = "TENANT_AUSENTE" });

                if (
                    !user.IsInRole(TenantRoles.Owner)
                    && !user.HasClaim("role", TenantRoles.Owner)
                    && !user.IsInRole(TenantRoles.Staff)
                )
                    return Results.Json(new { type = "SEM_PERMISSAO" }, statusCode: StatusCodes.Status403Forbidden);

                var ttl = TimeSpan.FromMinutes(15);
                var token = signedFicha.CreateToken(tenantId, orderId, ttl);
                var publicBase = config["PublicApiBaseUrl"] ?? config["ASPNETCORE_URLS"]?.Split(';')[0] ?? "";
                var url = string.IsNullOrWhiteSpace(publicBase)
                    ? $"/v1/fichas/{token}"
                    : $"{publicBase.TrimEnd('/')}/v1/fichas/{token}";

                return Results.Ok(new { url, expiresInSeconds = (int)ttl.TotalSeconds });
            }
        );

        // Download direto sem assinatura e bloqueado.
        studio.MapGet(
            "/direction-sheet",
            () =>
                Results.Json(
                    new
                    {
                        type = "FICHA_REQUER_URL_ASSINADA",
                        title = "Use POST .../direction-sheet-link e baixe via /v1/fichas/{token}.",
                    },
                    statusCode: StatusCodes.Status403Forbidden
                )
        );

        app.MapGet(
                "/v1/fichas/{token}",
                async (
                    string token,
                    ISignedFichaService signedFicha,
                    IOrderRepository orders,
                    IAnswerRepository answers,
                    IBriefingTemplateRepository templates,
                    IDirectionSheetRenderer renderer,
                    IPortfolioUploadService uploads,
                    MutableTenantContext tenantContext,
                    PrataDbContext db,
                    CancellationToken ct
                ) =>
                {
                    if (!signedFicha.TryValidate(token, out var tenantId, out var orderId))
                        return Results.Json(new { type = "FICHA_TOKEN_INVALIDO" }, statusCode: StatusCodes.Status403Forbidden);

                    tenantContext.Set(tenantId, "ficha-assinada", TenantStatus.Ativo);
                    await db.Database.OpenConnectionAsync(ct);
                    await db.Database.ExecuteSqlRawAsync(
                        "SELECT set_config('prata.tenant_id', {0}, true)",
                        tenantId.ToString()
                    );

                    var order = await orders.GetByIdAsync(tenantId, orderId, ct);
                    if (order is null)
                        return Results.NotFound();

                    var template = await templates.GetPublishedForServiceAsync(tenantId, order.ServiceTypeId, ct);
                    var existing = await answers.ListByOrderAsync(tenantId, orderId, ct);
                    var byCode = existing.ToDictionary(a => a.QuestionCode);

                    static string ReadText(Answer? a)
                    {
                        if (a is null)
                            return string.Empty;
                        try
                        {
                            using var doc = JsonDocument.Parse(a.ValueJson);
                            if (doc.RootElement.TryGetProperty("text", out var t))
                                return t.GetString() ?? string.Empty;
                            if (doc.RootElement.TryGetProperty("value", out var v))
                                return v.ToString();
                            if (doc.RootElement.TryGetProperty("option", out var o))
                                return o.GetString() ?? string.Empty;
                            if (doc.RootElement.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var asset in assets.EnumerateArray())
                                {
                                    if (asset.TryGetProperty("name", out var n) && n.GetString() is { Length: > 0 } name)
                                        return name;
                                }
                            }
                            return a.ValueJson;
                        }
                        catch (JsonException)
                        {
                            return string.Empty;
                        }
                    }

                    static string? ReadFirstAssetKey(Answer? a)
                    {
                        if (a is null)
                            return null;
                        try
                        {
                            using var doc = JsonDocument.Parse(a.ValueJson);
                            if (!doc.RootElement.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
                                return null;
                            foreach (var asset in assets.EnumerateArray())
                            {
                                if (asset.TryGetProperty("key", out var k) && k.GetString() is { Length: > 0 } key)
                                    return key;
                            }
                        }
                        catch (JsonException)
                        {
                            return null;
                        }

                        return null;
                    }

                    var b4 = ReadText(byCode.GetValueOrDefault("B4"));
                    var warmUp = int.TryParse(b4, out var b4n) && b4n < 3;
                    var attention = string.Join(
                        " · ",
                        new[]
                        {
                            ReadText(byCode.GetValueOrDefault("C2")),
                            ReadText(byCode.GetValueOrDefault("C3")),
                            ReadText(byCode.GetValueOrDefault("C4")),
                        }.Where(s => !string.IsNullOrWhiteSpace(s))
                    );

                    byte[]? b8Image = null;
                    var b8Answer = byCode.GetValueOrDefault("B8");
                    var b8Key = ReadFirstAssetKey(b8Answer);
                    if (!string.IsNullOrWhiteSpace(b8Key))
                        b8Image = await uploads.TryDownloadAsync(b8Key, ct);

                    var b8Caption = ReadText(b8Answer);

                    // URL assinada e so para staff autorizado — inclui nao-sensiveis + B8 referencia.
                    var items = existing
                        .Where(a => !a.IsSensitive)
                        .Select(a => new DirectionSheetItem(
                            a.QuestionCode,
                            a.PromptSnapshot,
                            ReadText(a),
                            Priority: a.QuestionCode.StartsWith('B') ? 1 : 2
                        ))
                        .ToList();

                    var pdf = renderer.Render(
                        new DirectionSheetModel(
                            order.Id,
                            template?.Name ?? "Servico",
                            order.IntendedDate,
                            items,
                            warmUp,
                            attention,
                            b8Image,
                            string.IsNullOrWhiteSpace(b8Caption) ? null : b8Caption
                        )
                    );

                    return Results.File(pdf, "application/pdf", $"ficha-{order.Id:N}.pdf");
                }
            )
            .WithTags("Ficha")
            .AllowAnonymous();

        app.MapGet(
                "/v1/studio/orders/{orderId:guid}/wa-link",
                async (
                    Guid orderId,
                    string? message,
                    ITenantContext tenant,
                    PrataDbContext db,
                    IWhatsAppLinkGenerator links,
                    CancellationToken ct
                ) =>
                {
                    if (tenant.TenantId is not Guid tenantId)
                        return Results.BadRequest(new { type = "TENANT_AUSENTE" });

                    var order = await db
                        .Orders.AsNoTracking()
                        .FirstOrDefaultAsync(o => o.Id == orderId && o.TenantId == tenantId, ct);
                    if (order is null)
                        return Results.NotFound();

                    var client = await db.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Id == order.ClientId, ct);
                    if (client?.WhatsApp is null)
                        return Results.BadRequest(new { type = "CLIENTE_SEM_WHATSAPP" });

                    var text =
                        message
                        ?? $"Ola {client.Name}, sobre o seu pedido no estudio — podemos alinhar por aqui?";
                    return Results.Ok(new { url = links.CreatePrefilledLink(client.WhatsApp, text) });
                }
            )
            .RequireAuthorization()
            .WithTags("StudioOrders");

        return app;
    }

    private static IResult Problem(Error error) =>
        Results.Json(new { type = error.Code, title = error.Message }, statusCode: StatusCodes.Status400BadRequest);
}

public sealed record AutosaveBody(string ValueJson);

public sealed record UploadRequest(string? ContentType);
