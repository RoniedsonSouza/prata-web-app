using AwesomeAssertions;
using Prata.Domain.Sales;

namespace Prata.Domain.Tests.Sales;

public class OrderAgendaTests
{
    [Fact]
    public void Confirmado_para_Agendado_grava_janela_logistica()
    {
        var pedido = PedidoEmConfirmado();
        var inicio = new DateTimeOffset(2026, 10, 20, 10, 0, 0, TimeSpan.Zero);
        var fim = inicio.AddHours(3);

        pedido.Agendar(inicio, fim, 45).IsSuccess.Should().BeTrue();
        pedido.Status.Should().Be(OrderStatus.Agendado);
        pedido.ScheduledStartsAt.Should().Be(inicio);
        pedido.TravelBufferMinutes.Should().Be(45);
    }

    [Fact]
    public void Agendado_pode_ir_para_Realizado()
    {
        var pedido = PedidoEmConfirmado();
        var inicio = pedido.IntendedDate.ToDateTime(TimeOnly.FromTimeSpan(TimeSpan.FromHours(9)), DateTimeKind.Utc);
        pedido.Agendar(new DateTimeOffset(inicio, TimeSpan.Zero), new DateTimeOffset(inicio.AddHours(2), TimeSpan.Zero), 30)
            .IsSuccess.Should()
            .BeTrue();

        var depoisDoEvento = new DateTimeOffset(pedido.IntendedDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddDays(1), TimeSpan.Zero);
        pedido.MarcarRealizado(depoisDoEvento).IsSuccess.Should().BeTrue();
        pedido.Status.Should().Be(OrderStatus.Realizado);
    }

    [Fact]
    public void Reagendar_de_Confirmado_vai_para_Reagendado()
    {
        var pedido = PedidoEmConfirmado();
        var nova = pedido.IntendedDate.AddDays(14);
        pedido.Reagendar(nova, PedidoBuilder.Agora).IsSuccess.Should().BeTrue();
        pedido.Status.Should().Be(OrderStatus.Reagendado);
        pedido.IntendedDate.Should().Be(nova);

        pedido.ReconfirmarAposReagendamento().IsSuccess.Should().BeTrue();
        pedido.Status.Should().Be(OrderStatus.Confirmado);
    }

    private static Order PedidoEmConfirmado()
    {
        var pedido = PedidoBuilder.Em(OrderStatus.Aprovado);
        pedido.Confirmar(sinalConfirmado: true, contratoAssinado: true, PedidoBuilder.Agora).IsSuccess.Should().BeTrue();
        return pedido;
    }
}
