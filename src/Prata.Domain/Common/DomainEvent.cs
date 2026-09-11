namespace Prata.Domain.Common;

/// <summary>
/// Evento de dominio levantado pela entidade. Publicado via outbox apos commit.
/// </summary>
public abstract record DomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();

    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}
