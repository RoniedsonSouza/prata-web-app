using Prata.Domain.Common;

namespace Prata.Application.Abstractions;

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IDateTimeProvider
{
    DateTimeOffset UtcNow { get; }
}

public sealed class SystemDateTimeProvider : IDateTimeProvider
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

public interface IEventPublisher
{
    Task PublishAsync(DomainEvent domainEvent, CancellationToken cancellationToken = default);
}

public interface IStorage
{
    Task<string> CreatePresignedUploadUrlAsync(
        string objectKey,
        string contentType,
        TimeSpan ttl,
        CancellationToken cancellationToken = default
    );

    Task<string> CreatePresignedDownloadUrlAsync(string objectKey, TimeSpan ttl, CancellationToken cancellationToken = default);
}

public interface IImageProcessor
{
    Task ProcessPortfolioDerivativesAsync(Stream original, string baseObjectKey, CancellationToken cancellationToken = default);
}

public interface INotifier
{
    Task SendEmailAsync(string to, string subject, string body, CancellationToken cancellationToken = default);
}

public interface IPdfRenderer
{
    Task<byte[]> RenderAsync(string templateKey, object model, CancellationToken cancellationToken = default);
}

public interface ISignatureProvider
{
    Task<Result<Unit>> RegistrarAceiteAsync(
        Guid contractId,
        string documentHash,
        string ip,
        string userAgent,
        DateTimeOffset at,
        CancellationToken cancellationToken = default
    );
}

/// <summary>
/// Port do PSP. Implementacao Asaas fica em Infrastructure (ADR-0005).
/// </summary>
public interface IPaymentGateway
{
    // Metodos concretos entram na E3. O port nasce na E1 para fechar a borda.
}
