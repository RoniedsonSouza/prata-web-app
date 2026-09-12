using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Prata.Application.Abstractions;
using Prata.Domain.Delivery;
using Prata.Infrastructure.Persistence;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Prata.Infrastructure.Imaging;

/// <summary>
/// Derivadas de galeria (RN-ENT-011) + registro de PhotoVariant + ZIP (RN-ENT-041).
/// </summary>
public sealed class GalleryOutboxProcessor(
    PrataDbContext db,
    IDateTimeProvider clock,
    IConfiguration configuration,
    ILogger<GalleryOutboxProcessor> logger
)
{
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Worker de fila: uma imagem/ZIP ruim nao pode derrubar o lote."
    )]
    public async Task<int> ProcessPendingAsync(CancellationToken cancellationToken = default)
    {
        var pending = await db
            .OutboxMessages.Where(m =>
                m.ProcessedAt == null
                && (m.Type == "GerarDerivadasGaleria" || m.Type == "MontarZipGaleria")
            )
            .OrderBy(m => m.OccurredAt)
            .Take(10)
            .ToListAsync(cancellationToken);

        var root =
            configuration["Storage:LocalRoot"]
            ?? Path.Combine(Path.GetTempPath(), "prata-storage");
        Directory.CreateDirectory(root);

        var processed = 0;
        foreach (var message in pending)
        {
            try
            {
                if (message.Type == "GerarDerivadasGaleria")
                    await ProcessDerivativesAsync(root, message.Payload, cancellationToken);
                else
                    await ProcessZipAsync(root, message.Payload, cancellationToken);

                message.ProcessedAt = clock.UtcNow;
                processed++;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Falha outbox galeria {MessageId} type={Type}", message.Id, message.Type);
            }
        }

        if (processed > 0)
            await db.SaveChangesAsync(cancellationToken);

        return processed;
    }

    private async Task ProcessDerivativesAsync(string root, string payload, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(payload);
        var objectKey = doc.RootElement.GetProperty("objectKey").GetString()
            ?? throw new InvalidOperationException("objectKey");
        var photoId = Guid.Parse(doc.RootElement.GetProperty("photoId").GetString()!);
        var galleryId = Guid.Parse(doc.RootElement.GetProperty("galleryId").GetString()!);

        var photo = await db.Photos.AsNoTracking().FirstOrDefaultAsync(p => p.Id == photoId && p.GalleryId == galleryId, ct);
        if (photo is null)
            return;

        var relative = objectKey.Replace('/', Path.DirectorySeparatorChar);
        var sourcePath = Path.Combine(root, relative);
        using var image = File.Exists(sourcePath)
            ? await Image.LoadAsync<Rgba32>(sourcePath, ct)
            : CreatePlaceholder();

        var agora = clock.UtcNow;
        await WriteAndRegisterAsync(photo.TenantId, photo.Id, image, objectKey + ".thumb.webp", PhotoVariantKind.Thumb, 480, false, agora, root, ct);
        await WriteAndRegisterAsync(photo.TenantId, photo.Id, image, objectKey + ".web.webp", PhotoVariantKind.Web, 1600, true, agora, root, ct);
        await WriteAndRegisterAsync(photo.TenantId, photo.Id, image, objectKey + ".texture.webp", PhotoVariantKind.Texture, 1024, false, agora, root, ct);
        await WriteAndRegisterAsync(photo.TenantId, photo.Id, image, objectKey + ".lqip.webp", PhotoVariantKind.Lqip, 20, false, agora, root, ct);
    }

    private async Task WriteAndRegisterAsync(
        Guid tenantId,
        Guid photoId,
        Image<Rgba32> image,
        string objectKey,
        PhotoVariantKind kind,
        int width,
        bool watermark,
        DateTimeOffset agora,
        string root,
        CancellationToken ct
    )
    {
        var exists = await db.PhotoVariants.AnyAsync(v => v.PhotoId == photoId && v.Kind == kind, ct);
        if (exists)
            return;

        var path = Path.Combine(root, objectKey.Replace('/', Path.DirectorySeparatorChar));
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        using var clone = image.Clone(ctx =>
            ctx.Resize(new ResizeOptions { Size = new Size(width, 0), Mode = ResizeMode.Max })
        );
        await clone.SaveAsync(path, new WebpEncoder { Quality = 80 }, ct);
        var info = new FileInfo(path);
        db.PhotoVariants.Add(
            PhotoVariant.Create(tenantId, photoId, kind, objectKey, clone.Width, clone.Height, info.Length, watermark, agora)
        );
    }

    private async Task ProcessZipAsync(string root, string payload, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(payload);
        var jobId = Guid.Parse(doc.RootElement.GetProperty("downloadJobId").GetString()!);
        var galleryId = Guid.Parse(doc.RootElement.GetProperty("galleryId").GetString()!);

        var job = await db.DownloadJobs.FirstOrDefaultAsync(j => j.Id == jobId, ct);
        if (job is null)
            return;

        var variants = await db
            .PhotoVariants.AsNoTracking()
            .Where(v =>
                v.TenantId == job.TenantId
                && v.Kind == PhotoVariantKind.Web
                && db.Photos.Any(p => p.Id == v.PhotoId && p.GalleryId == galleryId)
            )
            .ToListAsync(ct);

        var zipKey = $"tenants/{job.TenantId:N}/exports/{job.Id:N}.zip";
        var zipPath = Path.Combine(root, zipKey.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(zipPath)!);

        await using (var zipStream = File.Create(zipPath))
        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
        {
            var i = 0;
            foreach (var v in variants)
            {
                var entry = archive.CreateEntry($"foto-{++i:D4}.webp");
                await using var entryStream = entry.Open();
                var src = Path.Combine(root, v.StorageKey.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(src))
                {
                    await using var file = File.OpenRead(src);
                    await file.CopyToAsync(entryStream, ct);
                }
                else
                {
                    await entryStream.WriteAsync(System.Text.Encoding.UTF8.GetBytes("placeholder"), ct);
                }
            }
        }

        var bytes = new FileInfo(zipPath).Length;
        job.MarcarPronto(zipKey, bytes);
    }

    private static Image<Rgba32> CreatePlaceholder()
    {
        var image = new Image<Rgba32>(64, 64);
        image.Mutate(ctx => ctx.BackgroundColor(Color.ParseHex("cfc6b6")));
        return image;
    }
}
