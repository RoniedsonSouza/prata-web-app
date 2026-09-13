using Prata.Domain.Common;

namespace Prata.Domain.Scheduling;

public sealed class Availability : AggregateRoot, ITenantOwned
{
    private Availability() { }

    private Availability(Guid id, Guid tenantId, DayOfWeek weekday, TimeOnly startsAt, TimeOnly endsAt)
        : base(id)
    {
        TenantId = tenantId;
        Weekday = weekday;
        StartsAtTime = startsAt;
        EndsAtTime = endsAt;
    }

    public Guid TenantId { get; }

    public DayOfWeek Weekday { get; }

    public TimeOnly StartsAtTime { get; }

    public TimeOnly EndsAtTime { get; }

    public static Result<Availability> Create(Guid tenantId, DayOfWeek weekday, TimeOnly startsAt, TimeOnly endsAt)
    {
        if (tenantId == Guid.Empty)
            return Error.Validation("AVAILABILITY_TENANT", "Tenant obrigatorio.");
        if (endsAt <= startsAt)
            return Error.Validation("AVAILABILITY_INTERVALO_INVALIDO", "Horario de fim deve ser apos o inicio.");

        return new Availability(Guid.NewGuid(), tenantId, weekday, startsAt, endsAt);
    }

    public bool Covers(TimeOnly time) => time >= StartsAtTime && time < EndsAtTime;
}

public sealed class BlackoutDate : AggregateRoot, ITenantOwned
{
    private BlackoutDate()
    {
        Reason = null!;
    }

    private BlackoutDate(Guid id, Guid tenantId, DateOnly date, string reason)
        : base(id)
    {
        TenantId = tenantId;
        Date = date;
        Reason = reason;
    }

    public Guid TenantId { get; }

    public DateOnly Date { get; }

    public string Reason { get; }

    public static Result<BlackoutDate> Create(Guid tenantId, DateOnly date, string reason)
    {
        if (tenantId == Guid.Empty)
            return Error.Validation("BLACKOUT_TENANT", "Tenant obrigatorio.");
        if (date == default)
            return Error.Validation("BLACKOUT_DATA_INVALIDA", "Data de bloqueio obrigatoria.");
        if (string.IsNullOrWhiteSpace(reason))
            return Error.Validation("BLACKOUT_MOTIVO_OBRIGATORIO", "Motivo do bloqueio e obrigatorio.");

        return new BlackoutDate(Guid.NewGuid(), tenantId, date, reason.Trim());
    }

    public bool Bloqueia(DateTimeOffset instant) => DateOnly.FromDateTime(instant.UtcDateTime) == Date;

    public bool Bloqueia(DateOnly date) => Date == date;
}
