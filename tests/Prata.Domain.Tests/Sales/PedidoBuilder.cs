using AwesomeAssertions;
using Prata.Domain.Common;
using Prata.Domain.Sales;

namespace Prata.Domain.Tests.Sales;

/// <summary>
/// Monta pedido em um status alvo com precondicoes satisfeitas para as
/// transicoes permitidas daquele estado (docs/15 §2).
/// </summary>
internal static class PedidoBuilder
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ClientId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ServiceTypeId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    internal static readonly DateTimeOffset Agora = new(2026, 9, 11, 12, 0, 0, TimeSpan.Zero);

    public static Order Em(OrderStatus status)
    {
        var dataPretendida = DateOnly.FromDateTime(Agora.UtcDateTime.Date.AddDays(30));
        var pedido = Order.Create(TenantId, ClientId, ServiceTypeId, dataPretendida, Agora).Value;
        pedido
            .AdicionarItem(OrderItemKind.Package, Guid.NewGuid(), "Pacote Base", Money.Brl(1500m), 1)
            .IsSuccess.Should()
            .BeTrue();

        return status switch
        {
            OrderStatus.Rascunho => pedido,
            OrderStatus.Enviado => AvancarAte(pedido, OrderStatus.Enviado),
            OrderStatus.EmAnalise => AvancarAte(pedido, OrderStatus.EmAnalise),
            OrderStatus.OrcamentoEnviado => AvancarAte(pedido, OrderStatus.OrcamentoEnviado),
            OrderStatus.Aprovado => AvancarAte(pedido, OrderStatus.Aprovado),
            OrderStatus.Recusado => Recusado(pedido),
            OrderStatus.Expirado => Expirado(pedido),
            OrderStatus.EmEspera => EmEspera(pedido),
            OrderStatus.CanceladoPeloCliente => Cancelado(pedido),
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Estado nao coberto na E2."),
        };
    }

    private static Order AvancarAte(Order pedido, OrderStatus alvo)
    {
        if (alvo is OrderStatus.Enviado or OrderStatus.EmAnalise or OrderStatus.OrcamentoEnviado or OrderStatus.Aprovado)
            pedido.Enviar(pendenciasObrigatorias: []).IsSuccess.Should().BeTrue();

        if (alvo is OrderStatus.EmAnalise or OrderStatus.OrcamentoEnviado or OrderStatus.Aprovado)
            pedido.Analisar().IsSuccess.Should().BeTrue();

        if (alvo is OrderStatus.OrcamentoEnviado or OrderStatus.Aprovado)
            pedido.EnviarOrcamento(Agora, validadeDias: 7).IsSuccess.Should().BeTrue();

        if (alvo is OrderStatus.Aprovado)
            pedido.Aprovar(Agora).IsSuccess.Should().BeTrue();

        return pedido;
    }

    private static Order Recusado(Order pedido)
    {
        pedido.Enviar([]).IsSuccess.Should().BeTrue();
        pedido.Recusar("data indisponivel").IsSuccess.Should().BeTrue();
        return pedido;
    }

    private static Order Expirado(Order pedido)
    {
        AvancarAte(pedido, OrderStatus.OrcamentoEnviado);
        pedido.Expirar(Agora.AddDays(8)).IsSuccess.Should().BeTrue();
        return pedido;
    }

    private static Order EmEspera(Order pedido)
    {
        AvancarAte(pedido, OrderStatus.OrcamentoEnviado);
        pedido.ColocarEmEspera("aguardando decisao").IsSuccess.Should().BeTrue();
        return pedido;
    }

    private static Order Cancelado(Order pedido)
    {
        AvancarAte(pedido, OrderStatus.Aprovado);
        pedido.CancelarPeloCliente("desistencia").IsSuccess.Should().BeTrue();
        return pedido;
    }
}

internal static class OrderTestExtensions
{
    public static Result<Unit> Executar(this Order pedido, string transicao) =>
        transicao switch
        {
            "Enviar" => pedido.Enviar([]),
            "Analisar" => pedido.Analisar(),
            "Recusar" => pedido.Recusar("motivo de teste"),
            "EnviarOrcamento" => pedido.EnviarOrcamento(PedidoBuilder.Agora, 7),
            "RevisarOrcamento" => pedido.RevisarOrcamento(PedidoBuilder.Agora.AddDays(1), 7),
            "Aprovar" => pedido.Aprovar(PedidoBuilder.Agora),
            "Expirar" => pedido.Expirar(PedidoBuilder.Agora.AddDays(8)),
            "ColocarEmEspera" => pedido.ColocarEmEspera("motivo de teste"),
            "Retomar" => pedido.Retomar(),
            "CancelarPeloCliente" => pedido.CancelarPeloCliente("desistencia"),
            "Confirmar" => pedido.Confirmar(sinalConfirmado: true, contratoAssinado: true, PedidoBuilder.Agora),
            _ => throw new ArgumentOutOfRangeException(nameof(transicao), transicao, null),
        };
}
