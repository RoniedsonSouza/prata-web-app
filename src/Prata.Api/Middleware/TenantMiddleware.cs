using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Prata.Application.Abstractions;
using Prata.Domain.Tenancy;
using Prata.Infrastructure.Persistence;

namespace Prata.Api.Middleware;

/// <summary>
/// Resolve tenant pelo Host ({slug}.prata.app) ANTES da autenticacao.
/// </summary>
public sealed class TenantResolutionMiddleware(RequestDelegate next, ILogger<TenantResolutionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, ITenantContextAccessor tenantAccessor, PrataDbContext db)
    {
        if (IsPlatformPath(context.Request.Path))
        {
            await next(context);
            return;
        }

        var slug = ExtractSlug(context.Request);
        if (string.IsNullOrWhiteSpace(slug))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await context.Response.WriteAsJsonAsync(new { type = "TENANT_NAO_ENCONTRADO", title = "Tenant nao encontrado." });
            return;
        }

        var tenant = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Slug.Value == slug, context.RequestAborted);

        if (tenant is null)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await context.Response.WriteAsJsonAsync(new { type = "TENANT_NAO_ENCONTRADO", title = "Tenant nao encontrado." });
            return;
        }

        if (tenant.Status == TenantStatus.Suspenso && !context.Request.Path.StartsWithSegments("/health"))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { type = "TENANT_SUSPENSO", title = "Tenant suspenso." });
            return;
        }

        if (tenantAccessor is MutableTenantContext mutable)
            mutable.Set(tenant.Id, tenant.Slug.Value, tenant.Status);

        // Garante GUC mesmo se a conexao ja foi aberta na busca do tenant.
        await db.Database.ExecuteSqlRawAsync(
            "SELECT set_config('prata.tenant_id', {0}, false)",
            tenant.Id.ToString()
        );

        logger.LogDebug("Tenant resolvido {Slug} -> {TenantId}", slug, tenant.Id);
        await next(context);
    }

    private static bool IsPlatformPath(PathString path) =>
        path.StartsWithSegments("/health")
        || path.StartsWithSegments("/openapi")
        || path.StartsWithSegments("/scalar")
        || path.StartsWithSegments("/v1/platform")
        || path.StartsWithSegments("/v1/auth/studio/register");

    internal static string? ExtractSlug(HttpRequest request)
    {
        // Preferencia: header de desenvolvimento / path /t/{slug}
        if (request.Headers.TryGetValue("X-Tenant-Slug", out var header) && !string.IsNullOrWhiteSpace(header))
            return header.ToString().Trim().ToLowerInvariant();

        if (request.Path.StartsWithSegments("/v1/t", out var rest))
        {
            var segment = rest.Value?.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            return segment;
        }

        var host = request.Host.Host;
        if (host.EndsWith(".prata.app", StringComparison.OrdinalIgnoreCase))
        {
            var sub = host[..^".prata.app".Length];
            if (!string.IsNullOrWhiteSpace(sub) && !sub.Contains('.', StringComparison.Ordinal))
                return sub.ToLowerInvariant();
        }

        if (host is "localhost" or "127.0.0.1")
            return null;

        return null;
    }
}

/// <summary>
/// RN-TEN-011 — claim tenant_id do JWT deve casar com o host resolvido.
/// </summary>
public sealed class TenantGuardMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext)
    {
        if (context.User.Identity?.IsAuthenticated == true && tenantContext.IsResolved)
        {
            var claim = context.User.FindFirstValue("tenant_id");
            if (claim is not null && Guid.TryParse(claim, out var claimTenantId) && claimTenantId != tenantContext.TenantId)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(
                    new { type = "TENANT_TOKEN_DIVERGENTE", title = "Token nao pertence a este tenant." }
                );
                return;
            }
        }

        await next(context);
    }
}

public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Middleware de borda: converte qualquer falha em ProblemDetails sem vazar detalhe."
    )]
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception");
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(
                new
                {
                    type = "ERRO_INTERNO",
                    title = "Erro interno.",
                    status = 500,
                    // Sem detalhe de banco (E1 §3.7)
                }
            );
        }
    }
}
