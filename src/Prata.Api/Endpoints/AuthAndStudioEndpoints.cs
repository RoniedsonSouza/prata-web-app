using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Prata.Application.Abstractions;
using Prata.Domain.Catalog;
using Prata.Domain.Common;
using Prata.Domain.Showcase;
using Prata.Domain.Tenancy;
using Prata.Infrastructure.Identity;
using Prata.Infrastructure.Persistence;
using Prata.Infrastructure.Storage;

namespace Prata.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/auth").WithTags("Auth");

        group.MapPost(
            "/studio/register",
            async (StudioRegisterRequest body, IAuthService auth, CancellationToken ct) =>
            {
                var result = await auth.RegisterStudioAsync(
                    body.StudioName,
                    body.Slug,
                    body.OwnerName,
                    body.Email,
                    body.Password,
                    ct
                );
                return ToHttp(result);
            }
        );

        group.MapPost(
            "/login",
            async (LoginRequest body, IAuthService auth, CancellationToken ct) =>
            {
                var result = await auth.LoginAsync(body.Email, body.Password, ct);
                return ToHttp(result);
            }
        );

        group.MapPost(
            "/refresh",
            async (RefreshRequest body, IAuthService auth, CancellationToken ct) =>
            {
                var result = await auth.RefreshAsync(body.RefreshToken, ct);
                return ToHttp(result);
            }
        );

        group.MapPost(
            "/password-reset",
            async (PasswordResetRequestBody body, IAuthService auth, CancellationToken ct) =>
            {
                // Sem enumeracao: sempre 204.
                await auth.RequestPasswordResetAsync(body.Email, ct);
                return Results.NoContent();
            }
        );

        group.MapPost(
            "/password-reset/confirm",
            async (PasswordResetConfirmBody body, IAuthService auth, CancellationToken ct) =>
            {
                var result = await auth.ConfirmPasswordResetAsync(body.Token, body.NewPassword, ct);
                return result.IsSuccess ? Results.NoContent() : Problem(result.Error!.Value);
            }
        );

        group.MapPost(
            "/invites/accept",
            async (AcceptInviteRequest body, IAuthService auth, CancellationToken ct) =>
            {
                var result = await auth.AcceptInviteAsync(body.Token, body.Name, body.Password, ct);
                return ToHttp(result);
            }
        );

        return app;
    }

    private static IResult ToHttp(Result<AuthTokens> result) =>
        result.IsSuccess
            ? Results.Ok(
                new
                {
                    accessToken = result.Value.AccessToken,
                    refreshToken = result.Value.RefreshToken,
                    expiresAt = result.Value.AccessExpiresAt,
                }
            )
            : Problem(result.Error!.Value);

    private static IResult Problem(Error error) =>
        Results.Json(
            new
            {
                type = error.Code,
                title = error.Message,
            },
            statusCode: error.Code is "LOGIN_INVALIDO" or "REFRESH_INVALIDO" or "REFRESH_REUSADO"
                ? StatusCodes.Status401Unauthorized
                : error.Code is "CONTA_BLOQUEADA" or "ULTIMO_OWNER"
                    ? StatusCodes.Status409Conflict
                    : StatusCodes.Status400BadRequest
        );
}

public sealed record StudioRegisterRequest(
    string StudioName,
    string Slug,
    string OwnerName,
    string Email,
    string Password
);

public sealed record LoginRequest(string Email, string Password);

public sealed record RefreshRequest(string RefreshToken);

public sealed record PasswordResetRequestBody(string Email);

public sealed record PasswordResetConfirmBody(string Token, string NewPassword);

public sealed record AcceptInviteRequest(string Token, string Name, string Password);

public static class StudioEndpoints
{
    public static IEndpointRouteBuilder MapStudioEndpoints(this IEndpointRouteBuilder app)
    {
        var team = app.MapGroup("/v1/studio/team")
            .WithTags("Team")
            .RequireAuthorization();

        team.MapPost(
            "/invites",
            async (InviteRequest body, IAuthService auth, ClaimsPrincipal user, CancellationToken ct) =>
            {
                if (!user.IsInRole(TenantRoles.Owner) && !HasRoleClaim(user, TenantRoles.Owner))
                {
                    return Results.Json(
                        new { type = "SEM_PERMISSAO", title = "Apenas owner convida." },
                        statusCode: StatusCodes.Status403Forbidden
                    );
                }

                var result = await auth.InviteAsync(body.Email, body.Role, ct);
                return result.IsSuccess
                    ? Results.Created($"/v1/studio/team/invites/{result.Value}", new { id = result.Value })
                    : Results.Json(
                        new { type = result.Error!.Value.Code, title = result.Error.Value.Message },
                        statusCode: StatusCodes.Status400BadRequest
                    );
            }
        );

        team.MapDelete(
            "/{userId:guid}",
            async (Guid userId, IAuthService auth, ClaimsPrincipal user, CancellationToken ct) =>
            {
                if (!user.IsInRole(TenantRoles.Owner) && !HasRoleClaim(user, TenantRoles.Owner))
                {
                    return Results.Json(
                        new { type = "SEM_PERMISSAO", title = "Apenas owner remove membros." },
                        statusCode: StatusCodes.Status403Forbidden
                    );
                }

                var result = await auth.RemoveTeamMemberAsync(userId, ct);
                if (result.IsSuccess)
                {
                    return Results.NoContent();
                }

                var code = result.Error!.Value.Code;
                return Results.Json(
                    new { type = code, title = result.Error.Value.Message },
                    statusCode: code == "ULTIMO_OWNER"
                        ? StatusCodes.Status409Conflict
                        : StatusCodes.Status400BadRequest
                );
            }
        );

        var packages = app.MapGroup("/v1/studio/packages")
            .WithTags("Catalog")
            .RequireAuthorization();

        packages.MapGet(
            "/",
            async (PrataDbContext db, CancellationToken ct) =>
            {
                var list = await db
                    .Packages.AsNoTracking()
                    .OrderBy(p => p.Name)
                    .Select(p => new
                    {
                        p.Id,
                        p.ServiceTypeId,
                        p.Name,
                        Price = p.Price.HasValue ? (decimal?)p.Price.Value.Amount : null,
                        p.IncludedPhotos,
                        Status = p.Status.ToString(),
                    })
                    .ToListAsync(ct);
                return Results.Ok(list);
            }
        );

        packages.MapPost(
            "/",
            async (CreatePackageRequest body, PrataDbContext db, ITenantContext tenant, CancellationToken ct) =>
            {
                if (tenant.TenantId is null)
                {
                    return Results.BadRequest(new { type = "TENANT_NAO_RESOLVIDO" });
                }

                Money? price = body.Price is null ? null : Money.Brl(body.Price.Value);
                var created = Package.Create(
                    tenant.TenantId.Value,
                    body.ServiceTypeId,
                    body.Name,
                    price,
                    body.IncludedPhotos,
                    body.Description
                );
                if (created.IsFailure)
                {
                    return Results.BadRequest(
                        new { type = created.Error!.Value.Code, title = created.Error.Value.Message }
                    );
                }

                db.Packages.Add(created.Value);
                await db.SaveChangesAsync(ct);
                return Results.Created($"/v1/studio/packages/{created.Value.Id}", new { id = created.Value.Id });
            }
        );

        packages.MapPost(
            "/{id:guid}/publish",
            async (Guid id, PrataDbContext db, CancellationToken ct) =>
            {
                var package = await db.Packages.FirstOrDefaultAsync(p => p.Id == id, ct);
                if (package is null)
                {
                    return Results.NotFound();
                }

                var result = package.Publicar();
                if (result.IsFailure)
                {
                    return Results.BadRequest(
                        new { type = result.Error!.Value.Code, title = result.Error.Value.Message }
                    );
                }

                await db.SaveChangesAsync(ct);
                return Results.NoContent();
            }
        );

        packages.MapPost(
            "/{id:guid}/deactivate",
            async (Guid id, PrataDbContext db, CancellationToken ct) =>
            {
                var package = await db.Packages.FirstOrDefaultAsync(p => p.Id == id, ct);
                if (package is null)
                {
                    return Results.NotFound();
                }

                var result = package.Desativar();
                if (result.IsFailure)
                {
                    return Results.BadRequest(
                        new { type = result.Error!.Value.Code, title = result.Error.Value.Message }
                    );
                }

                await db.SaveChangesAsync(ct);
                return Results.NoContent();
            }
        );

        packages.MapPut(
            "/{id:guid}",
            async (Guid id, UpdatePackageRequest body, PrataDbContext db, CancellationToken ct) =>
            {
                var package = await db.Packages.FirstOrDefaultAsync(p => p.Id == id, ct);
                if (package is null)
                {
                    return Results.NotFound();
                }

                Money? price = body.Price is null ? null : Money.Brl(body.Price.Value);
                var result = package.Atualizar(body.Name, price, body.IncludedPhotos, body.Description);
                if (result.IsFailure)
                {
                    return Results.BadRequest(
                        new { type = result.Error!.Value.Code, title = result.Error.Value.Message }
                    );
                }

                await db.SaveChangesAsync(ct);
                return Results.NoContent();
            }
        );

        var addons = app.MapGroup("/v1/studio/addons")
            .WithTags("Catalog")
            .RequireAuthorization();

        addons.MapGet(
            "/",
            async (PrataDbContext db, CancellationToken ct) =>
            {
                var list = await db
                    .Addons.AsNoTracking()
                    .OrderBy(a => a.Name)
                    .Select(a => new
                    {
                        a.Id,
                        a.Name,
                        Price = a.Price.Amount,
                        a.IsActive,
                    })
                    .ToListAsync(ct);
                return Results.Ok(list);
            }
        );

        addons.MapPost(
            "/",
            async (CreateAddonRequest body, PrataDbContext db, ITenantContext tenant, CancellationToken ct) =>
            {
                if (tenant.TenantId is null)
                {
                    return Results.BadRequest(new { type = "TENANT_NAO_RESOLVIDO" });
                }

                var created = Addon.Create(tenant.TenantId.Value, body.Name, Money.Brl(body.Price));
                if (created.IsFailure)
                {
                    return Results.BadRequest(
                        new { type = created.Error!.Value.Code, title = created.Error.Value.Message }
                    );
                }

                db.Addons.Add(created.Value);
                await db.SaveChangesAsync(ct);
                return Results.Created($"/v1/studio/addons/{created.Value.Id}", new { id = created.Value.Id });
            }
        );

        addons.MapPost(
            "/{id:guid}/deactivate",
            async (Guid id, PrataDbContext db, CancellationToken ct) =>
            {
                var addon = await db.Addons.FirstOrDefaultAsync(a => a.Id == id, ct);
                if (addon is null)
                {
                    return Results.NotFound();
                }

                addon.Desativar();
                await db.SaveChangesAsync(ct);
                return Results.NoContent();
            }
        );

        var serviceTypes = app.MapGroup("/v1/studio/service-types")
            .WithTags("Catalog")
            .RequireAuthorization();

        serviceTypes.MapGet(
            "/",
            async (PrataDbContext db, CancellationToken ct) =>
            {
                var list = await db
                    .ServiceTypes.AsNoTracking()
                    .OrderBy(s => s.SortOrder)
                    .Select(s => new
                    {
                        s.Id,
                        s.Code,
                        s.Name,
                        s.IsActive,
                    })
                    .ToListAsync(ct);
                return Results.Ok(list);
            }
        );

        serviceTypes.MapPost(
            "/{id:guid}/deactivate",
            async (Guid id, PrataDbContext db, CancellationToken ct) =>
            {
                var st = await db.ServiceTypes.FirstOrDefaultAsync(s => s.Id == id, ct);
                if (st is null)
                {
                    return Results.NotFound();
                }

                st.Desativar();
                await db.SaveChangesAsync(ct);
                return Results.NoContent();
            }
        );

        var collections = app.MapGroup("/v1/studio/collections")
            .WithTags("Showcase")
            .RequireAuthorization();

        collections.MapGet(
            "/",
            async (PrataDbContext db, CancellationToken ct) =>
            {
                var list = await db
                    .Collections.AsNoTracking()
                    .OrderBy(c => c.Title)
                    .Select(c => new
                    {
                        c.Id,
                        c.Slug,
                        c.Title,
                        Status = c.Status.ToString(),
                    })
                    .ToListAsync(ct);
                return Results.Ok(list);
            }
        );

        collections.MapPost(
            "/",
            async (CreateCollectionRequest body, PrataDbContext db, ITenantContext tenant, CancellationToken ct) =>
            {
                if (tenant.TenantId is null)
                {
                    return Results.BadRequest(new { type = "TENANT_NAO_RESOLVIDO" });
                }

                var created = Collection.Create(tenant.TenantId.Value, body.Slug, body.Title);
                if (created.IsFailure)
                {
                    return Results.BadRequest(
                        new { type = created.Error!.Value.Code, title = created.Error.Value.Message }
                    );
                }

                db.Collections.Add(created.Value);
                await db.SaveChangesAsync(ct);
                return Results.Created(
                    $"/v1/studio/collections/{created.Value.Id}",
                    new { id = created.Value.Id }
                );
            }
        );

        collections.MapPost(
            "/{id:guid}/items",
            async (
                Guid id,
                AddCollectionItemRequest body,
                PrataDbContext db,
                ITenantContext tenant,
                CancellationToken ct
            ) =>
            {
                if (tenant.TenantId is null)
                {
                    return Results.BadRequest(new { type = "TENANT_NAO_RESOLVIDO" });
                }

                var collection = await db.Collections.FirstOrDefaultAsync(c => c.Id == id, ct);
                if (collection is null)
                {
                    return Results.NotFound();
                }

                var consent = Enum.TryParse<PortfolioConsent>(body.Consent, true, out var c)
                    ? c
                    : PortfolioConsent.Sim;
                var item = CollectionItem.Create(
                    tenant.TenantId.Value,
                    id,
                    body.ObjectKey,
                    body.AltText,
                    body.SortOrder,
                    consent,
                    body.IsCover
                );
                if (item.IsFailure)
                {
                    return Results.BadRequest(
                        new { type = item.Error!.Value.Code, title = item.Error.Value.Message }
                    );
                }

                db.CollectionItems.Add(item.Value);
                await db.SaveChangesAsync(ct);
                return Results.Created(
                    $"/v1/studio/collections/{id}/items/{item.Value.Id}",
                    new { id = item.Value.Id }
                );
            }
        );

        collections.MapPost(
            "/{id:guid}/cover/{itemId:guid}",
            async (Guid id, Guid itemId, PrataDbContext db, CancellationToken ct) =>
            {
                var collection = await db.Collections.FirstOrDefaultAsync(c => c.Id == id, ct);
                if (collection is null)
                {
                    return Results.NotFound();
                }

                var items = await db.CollectionItems.Where(i => i.CollectionId == id).ToListAsync(ct);
                foreach (var existing in items)
                {
                    collection.AdicionarItem(existing);
                }

                var result = collection.DefinirCapa(itemId);
                if (result.IsFailure)
                {
                    return Results.BadRequest(
                        new { type = result.Error!.Value.Code, title = result.Error.Value.Message }
                    );
                }

                await db.SaveChangesAsync(ct);
                return Results.NoContent();
            }
        );

        collections.MapPost(
            "/{id:guid}/publish",
            async (Guid id, PrataDbContext db, CancellationToken ct) =>
            {
                var collection = await db.Collections.FirstOrDefaultAsync(c => c.Id == id, ct);
                if (collection is null)
                {
                    return Results.NotFound();
                }

                // Itens nao sao navegados pelo Ignore — carrega CollectionItems.
                var items = await db.CollectionItems.Where(i => i.CollectionId == id).ToListAsync(ct);
                foreach (var item in items)
                {
                    collection.AdicionarItem(item);
                }

                var result = collection.Publicar();
                if (result.IsFailure)
                {
                    return Results.BadRequest(
                        new { type = result.Error!.Value.Code, title = result.Error.Value.Message }
                    );
                }

                await db.OutboxMessages.AddAsync(
                    new OutboxMessage
                    {
                        Id = Guid.NewGuid(),
                        TenantId = collection.TenantId,
                        Type = "ColecaoPublicada",
                        Payload =
                            $"{{\"collectionId\":\"{collection.Id}\",\"slug\":\"{collection.Slug}\",\"tenantId\":\"{collection.TenantId}\"}}",
                        OccurredAt = DateTimeOffset.UtcNow,
                    },
                    ct
                );
                await db.SaveChangesAsync(ct);
                return Results.NoContent();
            }
        );

        app.MapPost(
                "/v1/studio/portfolio/upload-url",
                async (
                    PortfolioUploadRequest body,
                    IPortfolioUploadService uploads,
                    ITenantContext tenant,
                    CancellationToken ct
                ) =>
                {
                    if (tenant.TenantId is null)
                    {
                        return Results.BadRequest(new { type = "TENANT_NAO_RESOLVIDO" });
                    }

                    var signed = await uploads.CreateSignedUploadAsync(
                        tenant.TenantId.Value,
                        body.FileName,
                        body.ContentType,
                        ct
                    );
                    return Results.Ok(
                        new
                        {
                            uploadUrl = signed.UploadUrl,
                            objectKey = signed.ObjectKey,
                            derivatives = signed.Derivatives,
                            expiresAt = signed.ExpiresAt,
                        }
                    );
                }
            )
            .RequireAuthorization()
            .RequireRateLimiting("tenant");

        app.MapPost(
                "/v1/studio/portfolio/upload-complete",
                async (
                    PortfolioUploadCompleteRequest body,
                    PrataDbContext db,
                    ITenantContext tenant,
                    CancellationToken ct
                ) =>
                {
                    if (tenant.TenantId is null)
                    {
                        return Results.BadRequest(new { type = "TENANT_NAO_RESOLVIDO" });
                    }

                    await db.OutboxMessages.AddAsync(
                        new OutboxMessage
                        {
                            Id = Guid.NewGuid(),
                            TenantId = tenant.TenantId,
                            Type = "GerarDerivadasPortfolio",
                            Payload =
                                $"{{\"objectKey\":{System.Text.Json.JsonSerializer.Serialize(body.ObjectKey)},\"tenantId\":\"{tenant.TenantId}\"}}",
                            OccurredAt = DateTimeOffset.UtcNow,
                        },
                        ct
                    );
                    await db.SaveChangesAsync(ct);
                    return Results.Accepted();
                }
            )
            .RequireAuthorization();

        return app;
    }

    private static bool HasRoleClaim(ClaimsPrincipal user, string role) =>
        user.Claims.Any(c =>
            (c.Type == ClaimTypes.Role || c.Type == "role")
            && string.Equals(c.Value, role, StringComparison.Ordinal)
        );
}

public sealed record InviteRequest(string Email, string Role);

public sealed record CreatePackageRequest(
    Guid ServiceTypeId,
    string Name,
    decimal? Price,
    int? IncludedPhotos,
    string? Description
);

public sealed record UpdatePackageRequest(
    string Name,
    decimal? Price,
    int? IncludedPhotos,
    string? Description
);

public sealed record CreateAddonRequest(string Name, decimal Price);

public sealed record CreateCollectionRequest(string Slug, string Title);

public sealed record AddCollectionItemRequest(
    string ObjectKey,
    string AltText,
    int SortOrder,
    string Consent,
    bool IsCover
);

public sealed record PortfolioUploadRequest(string FileName, string ContentType);

public sealed record PortfolioUploadCompleteRequest(string ObjectKey);

public static class PublicPortfolioEndpoints
{
    public static IEndpointRouteBuilder MapPublicPortfolioEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(
            "/v1/t/{slug}/services",
            async (string slug, PrataDbContext db, CancellationToken ct) =>
            {
                var tenant = await db
                    .Tenants.AsNoTracking()
                    .FirstOrDefaultAsync(t => t.Slug.Value == slug, ct);
                if (tenant is null)
                {
                    return Results.NotFound();
                }

                // Sem query filter de tenant no contexto ainda setado — usa Ignore + where.
                var packages = await db
                    .Packages.IgnoreQueryFilters()
                    .AsNoTracking()
                    .Where(p => p.TenantId == tenant.Id && p.Status == PackageStatus.Publicado)
                    .Select(p => new
                    {
                        p.Id,
                        p.Name,
                        Price = p.Price!.Value.Amount,
                        p.IncludedPhotos,
                    })
                    .ToListAsync(ct);

                return Results.Ok(packages);
            }
        );

        app.MapGet(
            "/v1/t/{slug}/portfolio",
            async (string slug, PrataDbContext db, CancellationToken ct) =>
            {
                var tenant = await db
                    .Tenants.AsNoTracking()
                    .FirstOrDefaultAsync(t => t.Slug.Value == slug, ct);
                if (tenant is null)
                {
                    return Results.NotFound();
                }

                var collections = await db
                    .Collections.IgnoreQueryFilters()
                    .AsNoTracking()
                    .Where(c => c.TenantId == tenant.Id && c.Status == CollectionStatus.Publicada)
                    .Select(c => new
                    {
                        c.Slug,
                        c.Title,
                    })
                    .ToListAsync(ct);

                var jsonLd = new
                {
                    @context = "https://schema.org",
                    @type = "LocalBusiness",
                    name = tenant.Name,
                    url = $"https://{slug}.prata.app",
                    image = collections
                        .Select(c => new
                        {
                            @type = "ImageObject",
                            name = c.Title,
                            url = $"https://{slug}.prata.app/portfolio/{c.Slug}",
                        })
                        .ToArray(),
                };

                return Results.Ok(new { collections, jsonLd });
            }
        );

        app.MapGet(
            "/v1/t/{slug}/og",
            async (string slug, PrataDbContext db, CancellationToken ct) =>
            {
                var tenant = await db
                    .Tenants.AsNoTracking()
                    .FirstOrDefaultAsync(t => t.Slug.Value == slug, ct);
                if (tenant is null)
                {
                    return Results.NotFound();
                }

                // Placeholder SVG ate @vercel/og no front; metadados OG ja apontam aqui.
                var svg =
                    $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"1200\" height=\"630\"><rect fill=\"#111\" width=\"100%\" height=\"100%\"/><text x=\"80\" y=\"320\" fill=\"#f5f5f5\" font-size=\"64\" font-family=\"Georgia, serif\">{System.Security.SecurityElement.Escape(tenant.Name)}</text></svg>";
                return Results.Content(svg, "image/svg+xml");
            }
        );

        app.MapGet(
            "/v1/t/{slug}/sitemap.xml",
            async (string slug, PrataDbContext db, CancellationToken ct) =>
            {
                var tenant = await db
                    .Tenants.AsNoTracking()
                    .FirstOrDefaultAsync(t => t.Slug.Value == slug, ct);
                if (tenant is null)
                {
                    return Results.NotFound();
                }

                var collections = await db
                    .Collections.IgnoreQueryFilters()
                    .AsNoTracking()
                    .Where(c => c.TenantId == tenant.Id && c.Status == CollectionStatus.Publicada)
                    .Select(c => c.Slug)
                    .ToListAsync(ct);

                var urls = string.Join(
                    "\n",
                    collections.Select(s =>
                        $"  <url><loc>https://{slug}.prata.app/portfolio/{s}</loc></url>"
                    )
                );
                var xml =
                    $"<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">\n{urls}\n</urlset>";
                return Results.Content(xml, "application/xml");
            }
        );

        app.MapGet(
            "/robots.txt",
            (HttpContext http) =>
            {
                var host = http.Request.Host.Host;
                var content =
                    $"User-agent: *\nAllow: /\nSitemap: https://{host}/v1/t/{host.Split('.')[0]}/sitemap.xml\n";
                return Results.Content(content, "text/plain");
            }
        );

        return app;
    }
}
