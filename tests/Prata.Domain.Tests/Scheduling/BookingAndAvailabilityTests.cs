using AwesomeAssertions;
using Prata.Domain.Billing;
using Prata.Domain.Common;
using Prata.Domain.Scheduling;

namespace Prata.Domain.Tests.Scheduling;

public class BookingAndAvailabilityTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid OrderId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly DateTimeOffset Start = new(2026, 10, 15, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset End = Start.AddHours(4);

    [Fact]
    public void RN_AGD_002_booking_so_nasce_de_pedido_confirmado()
    {
        var result = Booking.Create(
            TenantId,
            OrderId,
            Start,
            End,
            travelBufferMinutes: 30,
            orderStatusConfirmado: false
        );

        result.IsFailure.Should().BeTrue();
        result.Error!.Value.Code.Should().Be("BOOKING_PEDIDO_NAO_CONFIRMADO");
    }

    [Fact]
    public void RN_AGD_002_cria_booking_quando_pedido_confirmado()
    {
        var result = Booking.Create(TenantId, OrderId, Start, End, 30, orderStatusConfirmado: true);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(BookingStatus.Ativo);
        result.Value.TravelBufferMinutes.Should().Be(30);
    }

    [Fact]
    public void RN_AGD_001_intervalo_com_buffer_detecta_sobreposicao_no_dominio()
    {
        var a = Booking.Create(TenantId, OrderId, Start, End, 60, true).Value;
        var otherStart = End.AddMinutes(30); // dentro do buffer de 60
        var otherEnd = otherStart.AddHours(2);

        Booking
            .OverlapsConsideringBuffer(a.StartsAt, a.EndsAt, a.TravelBufferMinutes, otherStart, otherEnd, 0)
            .Should()
            .BeTrue();
    }

    [Fact]
    public void RN_AGD_010_blackout_exige_motivo_e_data()
    {
        BlackoutDate.Create(TenantId, default, "feriado").IsFailure.Should().BeTrue();
        BlackoutDate.Create(TenantId, new DateOnly(2026, 12, 25), " ").IsFailure.Should().BeTrue();

        var ok = BlackoutDate.Create(TenantId, new DateOnly(2026, 12, 25), "Natal");
        ok.IsSuccess.Should().BeTrue();
        ok.Value.Date.Should().Be(new DateOnly(2026, 12, 25));
    }

    [Fact]
    public void RN_AGD_010_blackout_bloqueia_data_do_booking()
    {
        var blackout = BlackoutDate.Create(TenantId, DateOnly.FromDateTime(Start.UtcDateTime), "bloqueio").Value;

        blackout.Bloqueia(Start).Should().BeTrue();
        blackout.Bloqueia(Start.AddDays(1)).Should().BeFalse();
    }

    [Fact]
    public void Availability_rejeita_intervalo_invalido()
    {
        Availability
            .Create(TenantId, DayOfWeek.Monday, new TimeOnly(18, 0), new TimeOnly(9, 0))
            .IsFailure.Should()
            .BeTrue();

        var ok = Availability.Create(TenantId, DayOfWeek.Saturday, new TimeOnly(9, 0), new TimeOnly(17, 0));
        ok.IsSuccess.Should().BeTrue();
        ok.Value.Covers(new TimeOnly(12, 0)).Should().BeTrue();
        ok.Value.Covers(new TimeOnly(18, 0)).Should().BeFalse();
    }

    [Fact]
    public void RN_AGD_020_cancelar_libera_status()
    {
        var booking = Booking.Create(TenantId, OrderId, Start, End, 30, true).Value;
        booking.Cancelar().IsSuccess.Should().BeTrue();
        booking.Status.Should().Be(BookingStatus.Cancelado);
        booking.Cancelar().IsFailure.Should().BeTrue();
    }

    [Fact]
    public void RN_AGD_020_reagendar_atualiza_janela_quando_ativo()
    {
        var booking = Booking.Create(TenantId, OrderId, Start, End, 30, true).Value;
        var novoInicio = Start.AddDays(7);
        var novoFim = novoInicio.AddHours(3);

        booking.Reagendar(novoInicio, novoFim, 45).IsSuccess.Should().BeTrue();
        booking.StartsAt.Should().Be(novoInicio);
        booking.TravelBufferMinutes.Should().Be(45);

        booking.Cancelar().IsSuccess.Should().BeTrue();
        booking.Reagendar(novoInicio.AddDays(1), novoFim.AddDays(1), 30).IsFailure.Should().BeTrue();
    }
}
