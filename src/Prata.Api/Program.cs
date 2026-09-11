using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Prata.Api.Endpoints;
using Prata.Api.Middleware;
using Prata.Infrastructure;
using Prata.Infrastructure.Persistence;
using Scalar.AspNetCore;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog(
        (ctx, services, cfg) =>
        {
            cfg.ReadFrom.Configuration(ctx.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Application", "Prata.Api")
                .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
                .Filter.ByExcluding(log =>
                {
                    var text = log.MessageTemplate.Text;
                    if (
                        text.Contains("password", StringComparison.OrdinalIgnoreCase)
                        || text.Contains("token", StringComparison.OrdinalIgnoreCase)
                        || text.Contains("authorization", StringComparison.OrdinalIgnoreCase)
                        || text.Contains("connectionstring", StringComparison.OrdinalIgnoreCase)
                        || text.Contains("signingkey", StringComparison.OrdinalIgnoreCase)
                    )
                    {
                        return true;
                    }

                    if (log.Properties.Values.Any(v =>
                            v.ToString().Contains("Bearer ", StringComparison.OrdinalIgnoreCase)
                            || v.ToString().Contains("Password=", StringComparison.OrdinalIgnoreCase)
                        ))
                    {
                        return true;
                    }

                    return false;
                });

            var seqUrl = ctx.Configuration["Seq:ServerUrl"];
            if (!string.IsNullOrWhiteSpace(seqUrl) && seqUrl != "CHANGE_ME")
            {
                cfg.WriteTo.Seq(
                    seqUrl,
                    apiKey: ctx.Configuration["Seq:ApiKey"],
                    formatProvider: CultureInfo.InvariantCulture
                );
            }
        }
    );

    builder.Services.AddPrataInfrastructure(builder.Configuration);
    builder.Services.AddOpenApi();
    builder.Services.AddProblemDetails();
    builder
        .Services.AddHealthChecks()
        .AddNpgSql(
            builder.Configuration.GetConnectionString(DependencyInjection.AppConnectionName)
                ?? builder.Configuration.GetConnectionString("Prata")
                ?? builder.Configuration.GetConnectionString("Default")
                ?? "Host=localhost;Database=prata;Username=prata_app;Password=prata_dev_only",
            name: "postgres"
        );

    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.AddPolicy(
            "tenant",
            httpContext =>
            {
                var tenant =
                    httpContext.RequestServices.GetService<Prata.Application.Abstractions.ITenantContext>();
                var key =
                    tenant?.TenantId?.ToString()
                    ?? httpContext.Connection.RemoteIpAddress?.ToString()
                    ?? "anon";
                return RateLimitPartition.GetFixedWindowLimiter(
                    key,
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 120,
                        Window = TimeSpan.FromMinutes(1),
                    }
                );
            }
        );
    });

    var app = builder.Build();

    app.UseMiddleware<ExceptionHandlingMiddleware>();
    app.UseSerilogRequestLogging();
    app.UseRateLimiter();
    app.UseMiddleware<TenantResolutionMiddleware>();
    app.UseAuthentication();
    app.UseMiddleware<TenantGuardMiddleware>();
    app.UseAuthorization();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.MapScalarApiReference();
    }

    app.MapHealthChecks("/health");
    app.MapHealthChecks(
        "/health/ready",
        new HealthCheckOptions { Predicate = check => check.Name == "postgres" }
    );

    app.MapAuthEndpoints();
    app.MapStudioEndpoints();
    app.MapPublicPortfolioEndpoints();
    app.MapPlatformAuthEndpoints();
    app.MapCriticalAuthorizationEndpoints();

    app.MapGet(
            "/v1/t/{slug}/theme",
            async (string slug, PrataDbContext db, CancellationToken ct) =>
            {
                var tenant = await db
                    .Tenants.AsNoTracking()
                    .FirstOrDefaultAsync(t => t.Slug.Value == slug, ct);
                if (tenant is null)
                    return Results.NotFound();

                var s = tenant.Settings;
                return Results.Ok(
                    new
                    {
                        theme = s.ThemeId.ToString().ToLowerInvariant(),
                        typePair = s.TypePair,
                        primaryColor = s.PrimaryColor,
                        accentColor = s.AccentColor,
                        effectsEnabled = s.EffectsEnabled,
                        css = $":root{{--prata-primary:{s.PrimaryColor};--prata-accent:{s.AccentColor};}}",
                    }
                );
            }
        )
        .RequireRateLimiting("tenant");

    app.MapGet(
            "/v1/platform/tenants",
            async (PrataDbContext db, CancellationToken ct) =>
            {
                var list = await db
                    .Tenants.IgnoreQueryFilters()
                    .AsNoTracking()
                    .OrderBy(t => t.Name)
                    .Select(t => new
                    {
                        t.Id,
                        Slug = t.Slug.Value,
                        t.Name,
                        Status = t.Status.ToString(),
                    })
                    .ToListAsync(ct);
                return Results.Ok(list);
            }
        )
        .RequireAuthorization("PlatformAdmin");

    app.MapGet(
            "/v1/platform/audit-logs",
            async (PrataDbContext db, CancellationToken ct) =>
            {
                var logs = await db
                    .AuditLogs.AsNoTracking()
                    .OrderByDescending(a => a.At)
                    .Take(100)
                    .Select(a => new
                    {
                        a.Id,
                        a.TenantId,
                        a.Action,
                        a.EntityType,
                        a.At,
                    })
                    .ToListAsync(ct);
                return Results.Ok(logs);
            }
        )
        .RequireAuthorization("PlatformAdmin");

    app.MapPost(
            "/v1/platform/tenants/{id:guid}/suspend",
            async (Guid id, PrataDbContext db, CancellationToken ct) =>
            {
                var tenant = await db
                    .Tenants.IgnoreQueryFilters()
                    .FirstOrDefaultAsync(t => t.Id == id, ct);
                if (tenant is null)
                    return Results.NotFound();

                var result = tenant.Suspender("plataforma");
                if (result.IsFailure)
                    return Results.Conflict(
                        new { type = result.Error!.Value.Code, title = result.Error.Value.Message }
                    );

                db.AuditLogs.Add(
                    new AuditLog
                    {
                        Id = Guid.NewGuid(),
                        TenantId = id,
                        Action = "SuspenderTenant",
                        EntityType = "Tenant",
                        EntityId = id,
                        At = DateTimeOffset.UtcNow,
                    }
                );
                await db.SaveChangesAsync(ct);
                return Results.NoContent();
            }
        )
        .RequireAuthorization("PlatformAdmin");

    app.MapPost(
        "/v1/internal/revalidate",
        async (HttpRequest request, Prata.Api.RevalidateBody body, IConfiguration config, CancellationToken ct) =>
        {
            var expected = config["Revalidate:Secret"] ?? "CHANGE_ME";
            if (
                !request.Headers.TryGetValue("X-Prata-Revalidate", out var presented)
                || presented != expected
                || expected == "CHANGE_ME"
            )
            {
                return Results.Unauthorized();
            }

            // Front consome via webhook; aqui so validamos o contrato E1.
            await Task.CompletedTask;
            return Results.Ok(new { revalidated = true, tags = body.Tags });
        }
    );

    app.MapGet(
        "/v1/platform/briefings/{id:guid}",
        () =>
            Results.Json(
                new { type = "SEM_PERMISSAO", title = "platform.admin nao acessa briefing." },
                statusCode: StatusCodes.Status403Forbidden
            )
    );

    app.MapGet(
        "/v1/platform/photos/{id:guid}",
        () =>
            Results.Json(
                new { type = "SEM_PERMISSAO", title = "platform.admin nao acessa foto." },
                statusCode: StatusCodes.Status403Forbidden
            )
    );

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "API encerrada inesperadamente");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program;

