using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Prata.Application.Abstractions;

namespace Prata.Infrastructure.Storage;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    public string Provider { get; set; } = "Dev";

    public string? ServiceUrl { get; set; }

    public string Region { get; set; } = "auto";

    public string AccessKey { get; set; } = string.Empty;

    public string SecretKey { get; set; } = string.Empty;

    public bool ForcePathStyle { get; set; } = true;

    public string BucketOriginals { get; set; } = "prata-originals";

    public int PresignedPutTtlMinutes { get; set; } = 60;
}

/// <summary>
/// Upload assinado S3/R2 quando Storage:Provider=S3; caso contrario Dev.
/// </summary>
public sealed class S3PortfolioUploadService(
    IAmazonS3 s3,
    IConfiguration configuration,
    IDateTimeProvider clock
) : IPortfolioUploadService
{
    public Task<SignedUpload> CreateSignedUploadAsync(
        Guid tenantId,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default
    )
    {
        _ = cancellationToken;
        var opts = configuration.GetSection(StorageOptions.SectionName).Get<StorageOptions>()
            ?? new StorageOptions();
        var safe = Path.GetFileName(fileName).Replace(' ', '-');
        var id = Guid.NewGuid().ToString("N");
        var baseKey = $"tenants/{tenantId:N}/portfolio/{id}/{safe}";
        var expires = clock.UtcNow.AddMinutes(opts.PresignedPutTtlMinutes);

        var request = new GetPreSignedUrlRequest
        {
            BucketName = opts.BucketOriginals,
            Key = baseKey,
            Verb = HttpVerb.PUT,
            Expires = expires.UtcDateTime,
            ContentType = contentType,
        };
        var url = s3.GetPreSignedURL(request);

        var derivatives = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["thumb"] = $"{baseKey}.thumb.webp",
            ["web"] = $"{baseKey}.web.webp",
            ["texture"] = $"{baseKey}.texture.webp",
            ["lqip"] = $"{baseKey}.lqip.webp",
        };

        return Task.FromResult(new SignedUpload(url, baseKey, derivatives, expires));
    }
}

public static class StorageServiceCollectionExtensions
{
    public static IServiceCollection AddPrataStorage(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        var opts =
            configuration.GetSection(StorageOptions.SectionName).Get<StorageOptions>()
            ?? new StorageOptions();
        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));

        if (
            string.Equals(opts.Provider, "S3", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(opts.AccessKey)
            && opts.AccessKey != "CHANGE_ME"
        )
        {
            services.AddSingleton<IAmazonS3>(_ =>
            {
                var config = new AmazonS3Config
                {
                    ForcePathStyle = opts.ForcePathStyle,
                    AuthenticationRegion = opts.Region,
                };
                if (!string.IsNullOrWhiteSpace(opts.ServiceUrl))
                {
                    config.ServiceURL = opts.ServiceUrl;
                }

                var credentials = new BasicAWSCredentials(opts.AccessKey, opts.SecretKey);
                return new AmazonS3Client(credentials, config);
            });
            services.AddSingleton<IPortfolioUploadService, S3PortfolioUploadService>();
        }
        else
        {
            services.AddSingleton<IPortfolioUploadService, DevPortfolioUploadService>();
        }

        return services;
    }
}
