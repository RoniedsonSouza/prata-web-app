using Prata.Application.Abstractions;

namespace Prata.Infrastructure.Storage;

/// <summary>
/// Upload assinado de portfolio (E1 antecipa worker de derivadas da E4).
/// </summary>
public interface IPortfolioUploadService
{
    Task<SignedUpload> CreateSignedUploadAsync(
        Guid tenantId,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default
    );

    /// <summary>Baixa bytes do objeto (ex.: embutir B8 na ficha). Null se ausente/Dev sem arquivo.</summary>
    Task<byte[]?> TryDownloadAsync(string objectKey, CancellationToken cancellationToken = default);

    Task<string> CreatePresignedDownloadUrlAsync(
        string objectKey,
        TimeSpan ttl,
        CancellationToken cancellationToken = default
    );
}

public sealed record SignedUpload(
    string UploadUrl,
    string ObjectKey,
    IReadOnlyDictionary<string, string> Derivatives,
    DateTimeOffset ExpiresAt
);

/// <summary>
/// Stub local: URL assinada fake apontando para /dev-upload. Produção usa R2/S3.
/// </summary>
public sealed class DevPortfolioUploadService(IDateTimeProvider clock) : IPortfolioUploadService
{
    public Task<SignedUpload> CreateSignedUploadAsync(
        Guid tenantId,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default
    )
    {
        var safe = Path.GetFileName(fileName).Replace(' ', '-');
        var id = Guid.NewGuid().ToString("N");
        var baseKey = $"tenants/{tenantId:N}/portfolio/{id}/{safe}";
        var expires = clock.UtcNow.AddMinutes(15);
        var derivatives = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["thumb"] = $"{baseKey}.thumb.webp",
            ["web"] = $"{baseKey}.web.webp",
            ["texture"] = $"{baseKey}.texture.webp",
            ["lqip"] = $"{baseKey}.lqip.webp",
        };

        return Task.FromResult(
            new SignedUpload(
                UploadUrl: $"https://upload.local.dev/{baseKey}?contentType={Uri.EscapeDataString(contentType)}&expires={expires.ToUnixTimeSeconds()}",
                ObjectKey: baseKey,
                Derivatives: derivatives,
                ExpiresAt: expires
            )
        );
    }

    public Task<byte[]?> TryDownloadAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        _ = objectKey;
        _ = cancellationToken;
        // Dev nao persiste bytes; ficha segue sem imagem embutida.
        return Task.FromResult<byte[]?>(null);
    }

    public Task<string> CreatePresignedDownloadUrlAsync(
        string objectKey,
        TimeSpan ttl,
        CancellationToken cancellationToken = default
    )
    {
        _ = cancellationToken;
        var expires = clock.UtcNow.Add(ttl).ToUnixTimeSeconds();
        return Task.FromResult(
            $"https://download.local.dev/{objectKey}?expires={expires}"
        );
    }
}
