using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Prata.Application.Abstractions;
using Prata.Domain.Common;
using Prata.Domain.Contracts;
using Prata.Domain.Scheduling;
using Prata.Infrastructure.Persistence;

namespace Prata.Infrastructure.Contracts;

public interface IContractService
{
    Task<Result<Guid>> CriarEEnviarAsync(
        Guid tenantId,
        Guid orderId,
        string templateVersion,
        int validadeDias,
        CancellationToken cancellationToken = default
    );

    Task<Result<Unit>> AssinarAsync(
        Guid tenantId,
        Guid contractId,
        string signerName,
        string signerEmail,
        string ip,
        string userAgent,
        CancellationToken cancellationToken = default
    );

    Task<Result<Guid>> CorrigirAsync(
        Guid tenantId,
        Guid contractId,
        string templateVersion,
        int validadeDias,
        CancellationToken cancellationToken = default
    );
}

public sealed class ContractService(
    PrataDbContext db,
    IDateTimeProvider clock,
    IPdfRenderer pdf,
    IUnitOfWork uow,
    ISignatureProvider signatureProvider
) : IContractService
{
    public async Task<Result<Guid>> CriarEEnviarAsync(
        Guid tenantId,
        Guid orderId,
        string templateVersion,
        int validadeDias,
        CancellationToken cancellationToken = default
    )
    {
        var created = Contract.Create(
            tenantId,
            orderId,
            templateVersion,
            ContractRequiredClauses.All.ToArray(),
            clock.UtcNow,
            validadeDias
        );
        if (created.IsFailure)
            return Result.Failure<Guid>(created.Error!.Value);

        var contract = created.Value;
        var model = new
        {
            OrderId = orderId,
            Clauses = ContractRequiredClauses.All,
            GalleryExpirationNote = "A galeria expira conforme prazo contratual do tenant.",
            ImageConsent = "Consentimento de uso de imagem conforme briefing.",
            MinorConsent = "Havendo menor, exige consentimento do responsavel.",
        };
        var bytes = await pdf.RenderAsync("contract", model, cancellationToken);
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var key = $"tenants/{tenantId}/contracts/{contract.Id}.pdf";

        var sent = contract.Enviar(key, hash, clock.UtcNow);
        if (sent.IsFailure)
            return Result.Failure<Guid>(sent.Error!.Value);

        db.Contracts.Add(contract);
        await uow.SaveChangesAsync(cancellationToken);
        return contract.Id;
    }

    public async Task<Result<Unit>> AssinarAsync(
        Guid tenantId,
        Guid contractId,
        string signerName,
        string signerEmail,
        string ip,
        string userAgent,
        CancellationToken cancellationToken = default
    )
    {
        var contract = await db
            .Contracts.Include(c => c.Signature)
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == contractId, cancellationToken);
        if (contract is null)
            return Error.Validation("CONTRATO_NAO_ENCONTRADO", "Contrato nao encontrado.");

        var result = contract.Assinar(
            signerName,
            signerEmail,
            contract.PdfSha256 ?? "",
            ip,
            userAgent,
            clock.UtcNow
        );
        if (result.IsFailure)
            return result;

        await signatureProvider.RegistrarAceiteAsync(
            contract.Id,
            contract.PdfSha256!,
            ip,
            userAgent,
            clock.UtcNow,
            cancellationToken
        );
        await uow.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }

    public async Task<Result<Guid>> CorrigirAsync(
        Guid tenantId,
        Guid contractId,
        string templateVersion,
        int validadeDias,
        CancellationToken cancellationToken = default
    )
    {
        var original = await db.Contracts.FirstOrDefaultAsync(
            c => c.TenantId == tenantId && c.Id == contractId,
            cancellationToken
        );
        if (original is null)
            return Error.Validation("CONTRATO_NAO_ENCONTRADO", "Contrato nao encontrado.");

        var novo = original.CriarCorrecao(templateVersion, ContractRequiredClauses.All.ToArray(), clock.UtcNow, validadeDias);
        if (novo.IsFailure)
            return Result.Failure<Guid>(novo.Error!.Value);

        db.Contracts.Add(novo.Value);
        await uow.SaveChangesAsync(cancellationToken);
        return novo.Value.Id;
    }
}

public sealed class OwnSignatureProvider : ISignatureProvider
{
    public Task<Result<Unit>> RegistrarAceiteAsync(
        Guid contractId,
        string documentHash,
        string ip,
        string userAgent,
        DateTimeOffset at,
        CancellationToken cancellationToken = default
    )
    {
        _ = (contractId, documentHash, ip, userAgent, at, cancellationToken);
        // Prova fica em Signature + AuditLog; port pronto para ZapSign (ADR-0006).
        return Task.FromResult<Result<Unit>>(Unit.Value);
    }
}

public sealed class SimpleContractPdfRenderer : IPdfRenderer
{
    public Task<byte[]> RenderAsync(string templateKey, object model, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        var text =
            $"{templateKey}\n{System.Text.Json.JsonSerializer.Serialize(model)}\n"
            + string.Join('\n', ContractRequiredClauses.All);
        return Task.FromResult(Encoding.UTF8.GetBytes(text));
    }
}

public interface ISchedulingService
{
    Task<Result<Unit>> UpsertAvailabilityAsync(
        Guid tenantId,
        DayOfWeek weekday,
        TimeOnly starts,
        TimeOnly ends,
        CancellationToken cancellationToken = default
    );

    Task<Result<Unit>> AddBlackoutAsync(Guid tenantId, DateOnly date, string reason, CancellationToken cancellationToken = default);

    Task<Result<Unit>> AgendarPedidoAsync(
        Guid tenantId,
        Guid orderId,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt,
        int travelBufferMinutes,
        CancellationToken cancellationToken = default
    );
}

public sealed class SchedulingService(PrataDbContext db, IOrderRepository orders, IUnitOfWork uow) : ISchedulingService
{
    public async Task<Result<Unit>> UpsertAvailabilityAsync(
        Guid tenantId,
        DayOfWeek weekday,
        TimeOnly starts,
        TimeOnly ends,
        CancellationToken cancellationToken = default
    )
    {
        var existing = await db.Availabilities.FirstOrDefaultAsync(
            a => a.TenantId == tenantId && a.Weekday == weekday,
            cancellationToken
        );
        if (existing is not null)
            db.Availabilities.Remove(existing);

        var created = Availability.Create(tenantId, weekday, starts, ends);
        if (created.IsFailure)
            return Result.Failure<Unit>(created.Error!.Value);

        db.Availabilities.Add(created.Value);
        await uow.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }

    public async Task<Result<Unit>> AddBlackoutAsync(
        Guid tenantId,
        DateOnly date,
        string reason,
        CancellationToken cancellationToken = default
    )
    {
        var hasBooking = await db.Bookings.AnyAsync(
            b =>
                b.TenantId == tenantId
                && b.Status == Domain.Billing.BookingStatus.Ativo
                && DateOnly.FromDateTime(b.StartsAt.UtcDateTime) == date,
            cancellationToken
        );
        if (hasBooking)
            return Error.Validation(
                "BLACKOUT_COM_RESERVA",
                "Nao e possivel bloquear data com booking ativo (RN-AGD-010)."
            );

        var created = BlackoutDate.Create(tenantId, date, reason);
        if (created.IsFailure)
            return Result.Failure<Unit>(created.Error!.Value);

        db.BlackoutDates.Add(created.Value);
        await uow.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }

    public async Task<Result<Unit>> AgendarPedidoAsync(
        Guid tenantId,
        Guid orderId,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt,
        int travelBufferMinutes,
        CancellationToken cancellationToken = default
    )
    {
        var order = await orders.GetByIdAsync(tenantId, orderId, cancellationToken);
        if (order is null)
            return Error.Validation("PEDIDO_NAO_ENCONTRADO", "Pedido nao encontrado.");

        var date = DateOnly.FromDateTime(startsAt.UtcDateTime);
        var blackout = await db.BlackoutDates.AnyAsync(b => b.TenantId == tenantId && b.Date == date, cancellationToken);
        if (blackout)
            return Error.Validation("BOOKING_DATA_BLOQUEADA", "Data bloqueada por BlackoutDate (RN-AGD-010).");

        var weekday = startsAt.UtcDateTime.DayOfWeek;
        var time = TimeOnly.FromDateTime(startsAt.UtcDateTime);
        var windows = await db.Availabilities.Where(a => a.TenantId == tenantId && a.Weekday == weekday).ToListAsync(cancellationToken);
        if (windows.Count > 0 && windows.All(w => !w.Covers(time)))
            return Error.Validation("AGENDA_FORA_DISPONIBILIDADE", "Horario fora da disponibilidade do tenant.");

        var agendado = order.Agendar(startsAt, endsAt, travelBufferMinutes);
        if (agendado.IsFailure)
            return agendado;

        var booking = await db.Bookings.FirstOrDefaultAsync(
            b => b.TenantId == tenantId && b.OrderId == orderId && b.Status == Domain.Billing.BookingStatus.Ativo,
            cancellationToken
        );
        if (booking is not null)
        {
            var reag = booking.Reagendar(startsAt, endsAt, travelBufferMinutes);
            if (reag.IsFailure)
                return reag;
        }

        await uow.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
