using Prata.Domain.Common;

namespace Prata.Domain.Billing;

/// <summary>Reserva minima de data (E3). Sobreposicao garantida por EXCLUDE no banco.</summary>
public sealed class Booking : AggregateRoot, ITenantOwned
{
    private Booking() { }

    private Booking(
        Guid id,
        Guid tenantId,
        Guid orderId,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt,
        int travelBufferMinutes
    )
        : base(id)
    {
        TenantId = tenantId;
        OrderId = orderId;
        StartsAt = startsAt;
        EndsAt = endsAt;
        TravelBufferMinutes = travelBufferMinutes;
        Status = BookingStatus.Ativo;
    }

    public Guid TenantId { get; }

    public Guid OrderId { get; }

    public DateTimeOffset StartsAt { get; }

    public DateTimeOffset EndsAt { get; }

    public int TravelBufferMinutes { get; }

    public BookingStatus Status { get; private set; }

    public static Result<Booking> Create(
        Guid tenantId,
        Guid orderId,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt,
        int travelBufferMinutes
    )
    {
        if (endsAt <= startsAt)
            return BillingErrors.IntervaloInvalido;
        if (travelBufferMinutes < 0)
            return Error.Validation("BOOKING_BUFFER_INVALIDO", "Buffer de deslocamento nao pode ser negativo.");

        return new Booking(Guid.NewGuid(), tenantId, orderId, startsAt, endsAt, travelBufferMinutes);
    }

    public Result<Unit> Cancelar()
    {
        if (Status != BookingStatus.Ativo)
            return Error.Validation("BOOKING_JA_CANCELADO", "Booking ja cancelado.");
        Status = BookingStatus.Cancelado;
        return Unit.Value;
    }
}

/// <summary>Evento bruto do PSP — idempotencia por ExternalEventId (RN-FIN-021).</summary>
public sealed class PaymentEvent : Entity, ITenantOwned
{
    private PaymentEvent()
    {
        ExternalEventId = null!;
        EventType = null!;
        PayloadJson = null!;
    }

    private PaymentEvent(
        Guid id,
        Guid tenantId,
        string externalEventId,
        string eventType,
        string payloadJson,
        DateTimeOffset receivedAt
    )
        : base(id)
    {
        TenantId = tenantId;
        ExternalEventId = externalEventId;
        EventType = eventType;
        PayloadJson = payloadJson;
        ReceivedAt = receivedAt;
    }

    public Guid TenantId { get; }

    public string ExternalEventId { get; }

    public string EventType { get; }

    public string PayloadJson { get; }

    public DateTimeOffset ReceivedAt { get; }

    public DateTimeOffset? ProcessedAt { get; private set; }

    public string? ProcessError { get; private set; }

    public string? DiscardedReason { get; private set; }

    public static PaymentEvent Create(
        Guid tenantId,
        string externalEventId,
        string eventType,
        string payloadJson,
        DateTimeOffset receivedAt
    ) =>
        new(Guid.NewGuid(), tenantId, externalEventId, eventType, payloadJson, receivedAt);

    public void MarcarProcessado(DateTimeOffset agora) => ProcessedAt = agora;

    public void MarcarDescartado(string reason, DateTimeOffset agora)
    {
        DiscardedReason = reason;
        ProcessedAt = agora;
    }

    public void MarcarErro(string error) => ProcessError = error;
}

public sealed class ReconciliationIssue : AggregateRoot, ITenantOwned
{
    private ReconciliationIssue()
    {
        Kind = null!;
        ExpectedJson = null!;
        FoundJson = null!;
    }

    private ReconciliationIssue(
        Guid id,
        Guid tenantId,
        Guid? paymentId,
        string kind,
        string expectedJson,
        string foundJson,
        DateTimeOffset openedAt
    )
        : base(id)
    {
        TenantId = tenantId;
        PaymentId = paymentId;
        Kind = kind;
        ExpectedJson = expectedJson;
        FoundJson = foundJson;
        OpenedAt = openedAt;
    }

    public Guid TenantId { get; }

    public Guid? PaymentId { get; }

    public string Kind { get; }

    public string ExpectedJson { get; }

    public string FoundJson { get; }

    public DateTimeOffset OpenedAt { get; }

    public DateTimeOffset? ResolvedAt { get; private set; }

    public Guid? ResolvedBy { get; private set; }

    public string? ResolutionNote { get; private set; }

    public static ReconciliationIssue Open(
        Guid tenantId,
        Guid? paymentId,
        string kind,
        string expectedJson,
        string foundJson,
        DateTimeOffset openedAt
    ) =>
        new(Guid.NewGuid(), tenantId, paymentId, kind, expectedJson, foundJson, openedAt);

    public Result<Unit> Resolver(Guid actorId, string note, DateTimeOffset agora)
    {
        if (ResolvedAt is not null)
            return Error.Validation("DIVERGENCIA_JA_RESOLVIDA", "Divergencia ja foi baixada.");
        if (string.IsNullOrWhiteSpace(note))
            return Error.Validation("DIVERGENCIA_NOTA_OBRIGATORIA", "Baixa manual exige nota.");

        ResolvedAt = agora;
        ResolvedBy = actorId;
        ResolutionNote = note.Trim();
        return Unit.Value;
    }
}
