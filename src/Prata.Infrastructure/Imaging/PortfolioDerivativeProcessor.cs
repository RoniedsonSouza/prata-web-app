using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Prata.Application.Abstractions;
using Prata.Infrastructure.Persistence;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Prata.Infrastructure.Imaging;

/// <summary>
/// Worker de derivadas de portfolio (E1 antecipa E4): thumb, web, texture, lqip.
/// </summary>
public sealed class PortfolioDerivativeProcessor(
    PrataDbContext db,
    IDateTimeProvider clock,
    IConfiguration configuration,
    ILogger<PortfolioDerivativeProcessor> logger
)
{
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Worker de fila: uma imagem ruim nao pode derrubar o lote."
    )]
    public async Task<int> ProcessPendingAsync(CancellationToken cancellationToken = default)
    {
        var pending = await db
            .OutboxMessages.Where(m =>
                m.ProcessedAt == null && m.Type == "GerarDerivadasPortfolio"
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
                await ProcessOneAsync(root, message.Payload, cancellationToken);
                message.ProcessedAt = clock.UtcNow;
                processed++;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Falha ao gerar derivadas {MessageId}", message.Id);
            }
        }

        if (processed > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return processed;
    }

    private static async Task ProcessOneAsync(
        string root,
        string payload,
        CancellationToken cancellationToken
    )
    {
        using var doc = JsonDocument.Parse(payload);
        var objectKey =
            doc.RootElement.GetProperty("objectKey").GetString()
            ?? throw new InvalidOperationException("objectKey ausente");
        var relative = objectKey.Replace('/', Path.DirectorySeparatorChar);
        var sourcePath = Path.Combine(root, relative);

        using var image = File.Exists(sourcePath)
            ? await Image.LoadAsync<Rgba32>(sourcePath, cancellationToken)
            : CreatePlaceholder();

        await WriteDerivativeAsync(image, sourcePath + ".thumb.webp", 480, cancellationToken);
        await WriteDerivativeAsync(image, sourcePath + ".web.webp", 1600, cancellationToken);
        await WriteDerivativeAsync(image, sourcePath + ".texture.webp", 1024, cancellationToken);
        await WriteDerivativeAsync(image, sourcePath + ".lqip.webp", 20, cancellationToken);
    }

    private static Image<Rgba32> CreatePlaceholder()
    {
        var image = new Image<Rgba32>(64, 64);
        image.Mutate(ctx => ctx.BackgroundColor(Color.ParseHex("cfc6b6")));
        return image;
    }

    private static async Task WriteDerivativeAsync(
        Image<Rgba32> image,
        string path,
        int width,
        CancellationToken cancellationToken
    )
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        using var clone = image.Clone(ctx =>
            ctx.Resize(new ResizeOptions { Size = new Size(width, 0), Mode = ResizeMode.Max })
        );
        await clone.SaveAsync(path, new WebpEncoder { Quality = 80 }, cancellationToken);
    }
}
