using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Prata.Application.Abstractions;
using Prata.Domain.Common;
using Prata.Domain.Delivery;
using Prata.Infrastructure.Persistence;
using Prata.Infrastructure.Storage;

namespace Prata.Api.Endpoints;

public static class DeliveryEndpoints
{
    public static IEndpointRouteBuilder MapDeliveryEndpoints(this IEndpointRouteBuilder app)
    {
        var studio = app.MapGroup("/v1/studio/galleries").WithTags("StudioGalleries").RequireAuthorization();

        studio.MapPost(
            "/",
            async (CreateGalleryBody body, ITenantContext tenant, PrataDbContext db, IDateTimeProvider clock, CancellationToken ct) =>
            {
                if (tenant.TenantId is not Guid tenantId)
                    return Results.BadRequest(new { type = "TENANT_AUSENTE" });

                var consent = Enum.TryParse<PortfolioConsent>(body.PortfolioConsent, true, out var c)
                    ? c
                    : PortfolioConsent.Sim;

                var created = Gallery.Create(
                    tenantId,
                    body.OrderId,
                    body.PhotoLimit,
                    consent,
                    body.HasMinor,
                    clock.UtcNow.AddMonths(body.ExpirationMonths <= 0 ? 12 : body.ExpirationMonths),
                    clock.UtcNow
                );
                if (created.IsFailure)
                    return Results.BadRequest(new { type = created.Error!.Value.Code, title = created.Error.Value.Message });

                db.Galleries.Add(created.Value);
                await db.SaveChangesAsync(ct);
                return Results.Created($"/v1/studio/galleries/{created.Value.Id}", new { id = created.Value.Id });
            }
        );

        studio.MapGet(
            "/{id:guid}",
            async (Guid id, ITenantContext tenant, PrataDbContext db, CancellationToken ct) =>
            {
                if (tenant.TenantId is not Guid tenantId)
                    return Results.BadRequest(new { type = "TENANT_AUSENTE" });

                var g = await db
                    .Galleries.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId, ct);
                if (g is null)
                    return Results.NotFound();

                var photos = await db.Photos.AsNoTracking().CountAsync(p => p.GalleryId == id, ct);
                return Results.Ok(
                    new
                    {
                        g.Id,
                        Status = g.Status.ToString(),
                        g.PhotoLimit,
                        g.OrderId,
                        photoCount = photos,
                        g.PermiteAltaResolucao,
                        g.PermiteVisualizacaoBaixa,
                    }
                );
            }
        );

        studio.MapGet(
            "/{id:guid}/photos",
            async (
                Guid id,
                ITenantContext tenant,
                PrataDbContext db,
                IPortfolioUploadService uploads,
                CancellationToken ct
            ) =>
            {
                if (tenant.TenantId is not Guid tenantId)
                    return Results.BadRequest(new { type = "TENANT_AUSENTE" });

                var g = await db
                    .Galleries.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId, ct);
                if (g is null)
                    return Results.NotFound();

                var photos = await db
                    .Photos.AsNoTracking()
                    .Where(p => p.GalleryId == id && p.TenantId == tenantId)
                    .OrderBy(p => p.SortOrder)
                    .ToListAsync(ct);
                var ids = photos.Select(p => p.Id).ToList();
                var variants = await db
                    .PhotoVariants.AsNoTracking()
                    .Where(v => ids.Contains(v.PhotoId))
                    .ToListAsync(ct);

                var ttl = TimeSpan.FromMinutes(15);
                var list = new List<object>();
                foreach (var p in photos)
                {
                    var thumb = variants.FirstOrDefault(v => v.PhotoId == p.Id && v.Kind == PhotoVariantKind.Thumb);
                    var web = variants.FirstOrDefault(v => v.PhotoId == p.Id && v.Kind == PhotoVariantKind.Web);
                    var kind = g.PermiteAltaResolucao ? web ?? thumb : thumb ?? web;
                    string? url = null;
                    if (kind is not null)
                        url = await uploads.CreatePresignedDownloadUrlAsync(kind.StorageKey, ttl, ct);

                    list.Add(
                        new
                        {
                            p.Id,
                            p.Width,
                            p.Height,
                            p.SortOrder,
                            src = url,
                            watermarked = kind?.HasWatermark ?? false,
                            lowResOnly = !g.PermiteAltaResolucao,
                        }
                    );
                }

                return Results.Ok(list);
            }
        );

        studio.MapPost(
            "/{id:guid}/photos/upload-url",
            async (
                Guid id,
                UploadPhotoBody body,
                ITenantContext tenant,
                PrataDbContext db,
                IPortfolioUploadService uploads,
                CancellationToken ct
            ) =>
            {
                if (tenant.TenantId is not Guid tenantId)
                    return Results.BadRequest(new { type = "TENANT_AUSENTE" });

                var g = await db.Galleries.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId, ct);
                if (g is null)
                    return Results.NotFound();

                // RN-ENT-001 / RN-ENT-004 — autoriza primeiro, depois assina.
                var signed = await uploads.CreateSignedUploadAsync(
                    tenantId,
                    body.FileName ?? $"photo-{Guid.NewGuid():N}.jpg",
                    body.ContentType ?? "image/jpeg",
                    ct
                );
                return Results.Ok(
                    new
                    {
                        objectKey = signed.ObjectKey,
                        uploadUrl = signed.UploadUrl,
                        expiresAt = signed.ExpiresAt,
                        derivatives = signed.Derivatives,
                    }
                );
            }
        );

        studio.MapPost(
            "/{id:guid}/photos",
            async (
                Guid id,
                RegisterPhotoBody body,
                ITenantContext tenant,
                PrataDbContext db,
                CancellationToken ct
            ) =>
            {
                if (tenant.TenantId is not Guid tenantId)
                    return Results.BadRequest(new { type = "TENANT_AUSENTE" });

                var g = await db
                    .Galleries.Include("_photos")
                    .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId, ct);
                if (g is null)
                    return Results.NotFound();

                var photo = Photo.Create(
                    tenantId,
                    id,
                    body.ObjectKey,
                    body.Bytes,
                    body.Hash,
                    body.Width,
                    body.Height,
                    body.SortOrder
                );
                if (photo.IsFailure)
                    return Results.BadRequest(new { type = photo.Error!.Value.Code, title = photo.Error.Value.Message });

                var add = g.AdicionarFoto(photo.Value);
                if (add.IsFailure)
                    return Results.BadRequest(new { type = add.Error!.Value.Code, title = add.Error.Value.Message });

                db.OutboxMessages.Add(
                    new OutboxMessage
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        Type = "GerarDerivadasGaleria",
                        Payload =
                            $"{{\"galleryId\":\"{id}\",\"photoId\":\"{photo.Value.Id}\",\"objectKey\":{System.Text.Json.JsonSerializer.Serialize(body.ObjectKey)}}}",
                        OccurredAt = DateTimeOffset.UtcNow,
                    }
                );

                await db.SaveChangesAsync(ct);
                return Results.Created($"/v1/studio/galleries/{id}/photos/{photo.Value.Id}", new { id = photo.Value.Id });
            }
        );

        studio.MapPost(
            "/{id:guid}/publish",
            async (Guid id, ITenantContext tenant, PrataDbContext db, CancellationToken ct) =>
            {
                if (tenant.TenantId is not Guid tenantId)
                    return Results.BadRequest(new { type = "TENANT_AUSENTE" });

                var g = await db.Galleries.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId, ct);
                if (g is null)
                    return Results.NotFound();

                var photoIds = await db
                    .Photos.AsNoTracking()
                    .Where(p => p.GalleryId == id && p.TenantId == tenantId)
                    .Select(p => p.Id)
                    .ToListAsync(ct);
                if (photoIds.Count == 0)
                    return Results.BadRequest(
                        new { type = "GALERIA_SEM_FOTOS", title = "Galeria sem fotos para publicar." }
                    );

                var withThumbWeb = await db
                    .PhotoVariants.AsNoTracking()
                    .Where(v =>
                        photoIds.Contains(v.PhotoId)
                        && (v.Kind == PhotoVariantKind.Thumb || v.Kind == PhotoVariantKind.Web)
                    )
                    .GroupBy(v => v.PhotoId)
                    .CountAsync(g2 => g2.Select(x => x.Kind).Distinct().Count() >= 2, ct);

                var ready = withThumbWeb == photoIds.Count;
                var result = g.Disponibilizar(ready);
                if (result.IsFailure)
                    return Results.BadRequest(new { type = result.Error!.Value.Code, title = result.Error.Value.Message });

                await db.SaveChangesAsync(ct);
                return Results.NoContent();
            }
        );

        studio.MapPost(
            "/{id:guid}/share-links",
            async (
                Guid id,
                CreateShareBody body,
                ITenantContext tenant,
                PrataDbContext db,
                IDateTimeProvider clock,
                CancellationToken ct
            ) =>
            {
                if (tenant.TenantId is not Guid tenantId)
                    return Results.BadRequest(new { type = "TENANT_AUSENTE" });

                var exists = await db.Galleries.AnyAsync(g => g.Id == id && g.TenantId == tenantId, ct);
                if (!exists)
                    return Results.NotFound();

                var token = Convert.ToHexString(Guid.NewGuid().ToByteArray());
                var link = ShareLink.Create(
                    tenantId,
                    id,
                    body.Password,
                    token,
                    clock.UtcNow.AddDays(body.ExpiresInDays <= 0 ? 30 : body.ExpiresInDays),
                    body.AllowFavorites,
                    allowWebDownload: body.AllowWebDownload ?? true,
                    clock.UtcNow
                );
                if (link.IsFailure)
                    return Results.BadRequest(new { type = link.Error!.Value.Code, title = link.Error.Value.Message });

                db.ShareLinks.Add(link.Value);
                await db.SaveChangesAsync(ct);
                // Token em claro so nesta resposta.
                return Results.Ok(new { id = link.Value.Id, token, allowWebDownload = link.Value.AllowWebDownload });
            }
        );

        studio.MapPost(
            "/{id:guid}/downloads",
            async (
                Guid id,
                DownloadBody body,
                ClaimsPrincipal user,
                ITenantContext tenant,
                PrataDbContext db,
                IDateTimeProvider clock,
                CancellationToken ct
            ) =>
            {
                if (tenant.TenantId is not Guid tenantId)
                    return Results.BadRequest(new { type = "TENANT_AUSENTE" });

                var g = await db.Galleries.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId, ct);
                if (g is null)
                    return Results.NotFound();

                var userId = Guid.TryParse(user.FindFirstValue("sub"), out var uid) ? uid : Guid.Empty;
                var job = DownloadJob.Create(
                    tenantId,
                    id,
                    body.Scope ?? "selection",
                    userId,
                    clock.UtcNow.AddDays(2),
                    clock.UtcNow
                );
                db.DownloadJobs.Add(job);
                db.OutboxMessages.Add(
                    new OutboxMessage
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        Type = "MontarZipGaleria",
                        Payload = $"{{\"downloadJobId\":\"{job.Id}\",\"galleryId\":\"{id}\"}}",
                        OccurredAt = clock.UtcNow,
                    }
                );
                await db.SaveChangesAsync(ct);
                // RN-ENT-041 — 202 com id do job.
                return Results.Accepted($"/v1/studio/downloads/{job.Id}", new { jobId = job.Id });
            }
        );

        var portal = app.MapGroup("/v1/portal/galleries").WithTags("PortalGalleries").RequireRateLimiting("tenant");

        portal.MapGet(
            "/{id:guid}",
            async (Guid id, ITenantContext tenant, PrataDbContext db, CancellationToken ct) =>
            {
                if (tenant.TenantId is not Guid tenantId)
                    return Results.BadRequest(new { type = "TENANT_AUSENTE" });

                var g = await db
                    .Galleries.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId, ct);
                if (g is null)
                    return Results.NotFound();

                return Results.Ok(
                    new
                    {
                        g.Id,
                        Status = g.Status.ToString(),
                        g.PhotoLimit,
                        g.PermiteAltaResolucao,
                        g.PermiteVisualizacaoBaixa,
                        blocked = g.Status == GalleryStatus.BloqueadaPorPendencia,
                    }
                );
            }
        );

        portal.MapGet(
            "/{id:guid}/photos",
            async (
                Guid id,
                ITenantContext tenant,
                PrataDbContext db,
                IPortfolioUploadService uploads,
                CancellationToken ct
            ) =>
            {
                if (tenant.TenantId is not Guid tenantId)
                    return Results.BadRequest(new { type = "TENANT_AUSENTE" });

                var g = await db
                    .Galleries.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId, ct);
                if (g is null || !g.PermiteVisualizacaoBaixa)
                    return Results.NotFound();

                var photos = await db
                    .Photos.AsNoTracking()
                    .Where(p => p.GalleryId == id)
                    .OrderBy(p => p.SortOrder)
                    .ToListAsync(ct);
                var ids = photos.Select(p => p.Id).ToList();
                var variants = await db
                    .PhotoVariants.AsNoTracking()
                    .Where(v =>
                        ids.Contains(v.PhotoId)
                        && (
                            v.Kind == PhotoVariantKind.Thumb
                            || (g.PermiteAltaResolucao && v.Kind == PhotoVariantKind.Web)
                        )
                    )
                    .ToListAsync(ct);

                var ttl = TimeSpan.FromMinutes(10);
                var list = new List<object>();
                foreach (var p in photos)
                {
                    var preferred =
                        g.PermiteAltaResolucao
                            ? variants.FirstOrDefault(v => v.PhotoId == p.Id && v.Kind == PhotoVariantKind.Web)
                                ?? variants.FirstOrDefault(v => v.PhotoId == p.Id && v.Kind == PhotoVariantKind.Thumb)
                            : variants.FirstOrDefault(v => v.PhotoId == p.Id && v.Kind == PhotoVariantKind.Thumb);

                    if (preferred is null)
                        continue;

                    var url = await uploads.CreatePresignedDownloadUrlAsync(preferred.StorageKey, ttl, ct);
                    list.Add(
                        new
                        {
                            p.Id,
                            src = url,
                            alt = $"Foto {p.SortOrder + 1}",
                            width = preferred.Width,
                            height = preferred.Height,
                        }
                    );
                }

                return Results.Ok(list);
            }
        );

        return app;
    }
}

public sealed record CreateGalleryBody(
    Guid OrderId,
    int PhotoLimit,
    string? PortfolioConsent,
    bool HasMinor,
    int ExpirationMonths
);

public sealed record UploadPhotoBody(string? FileName, string? ContentType);

public sealed record RegisterPhotoBody(
    string ObjectKey,
    long Bytes,
    string Hash,
    int Width,
    int Height,
    int SortOrder
);

public sealed record CreateShareBody(
    string Password,
    int ExpiresInDays,
    bool AllowFavorites,
    bool? AllowWebDownload
);

public sealed record DownloadBody(string? Scope);
