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
    /// <summary>Envio simples (ex.: reset de senha) sem idempotencia de negocio.</summary>
    Task SendEmailAsync(string to, string subject, string body, CancellationToken cancellationToken = default);

    /// <summary>
    /// E-mail transacional com idempotencia (tenant, destinatario, tipo, chave) — RN-NOT-001.
    /// Falha de SMTP nao lanca — RN-NOT-003.
    /// </summary>
    Task SendTransactionalEmailAsync(
        Guid tenantId,
        string to,
        string type,
        string idempotencyKey,
        string subject,
        string body,
        CancellationToken cancellationToken = default
    );
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

/// <summary>Repositorio de Client. Unicidade (tenant_id, email) na persistencia — RN-COM-012.</summary>
public interface IClientRepository
{
    Task<Prata.Domain.Sales.Client?> GetByEmailAsync(
        Guid tenantId,
        string email,
        CancellationToken cancellationToken = default
    );

    Task<Prata.Domain.Sales.Client?> GetByIdAsync(
        Guid tenantId,
        Guid clientId,
        CancellationToken cancellationToken = default
    );

    Task AddAsync(Prata.Domain.Sales.Client client, CancellationToken cancellationToken = default);
}

/// <summary>Repositorio de Order. Queries de lista no back-office usam Dapper com TenantId explicito.</summary>
public interface IOrderRepository
{
    Task<Prata.Domain.Sales.Order?> GetByIdAsync(
        Guid tenantId,
        Guid orderId,
        CancellationToken cancellationToken = default
    );

    Task AddAsync(Prata.Domain.Sales.Order order, CancellationToken cancellationToken = default);

    /// <summary>Datas de pedidos Confirmado do tenant — substituto da agenda ate a E5 (RN-AGD-030).</summary>
    Task<IReadOnlyList<DateOnly>> GetConfirmedIntendedDatesAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default
    );
}
