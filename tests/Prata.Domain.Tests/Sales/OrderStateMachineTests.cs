using AwesomeAssertions;
using Prata.Domain.Sales;

namespace Prata.Domain.Tests.Sales;

/// <summary>
/// RN-COM-030 — percorre todos os pares estado × transição da tabela E2
/// (até Aprovado) e afirma que só os pares permitidos passam.
/// </summary>
public class OrderStateMachineTests
{
    public static TheoryData<OrderStatus, string> TodosOsParesDeTransicao()
    {
        var estadosE2 = new[]
        {
            OrderStatus.Rascunho,
            OrderStatus.Enviado,
            OrderStatus.EmAnalise,
            OrderStatus.OrcamentoEnviado,
            OrderStatus.Aprovado,
            OrderStatus.Recusado,
            OrderStatus.Expirado,
            OrderStatus.EmEspera,
            OrderStatus.CanceladoPeloCliente,
        };

        var transicoes = new[]
        {
            "Enviar",
            "Analisar",
            "Recusar",
            "EnviarOrcamento",
            "RevisarOrcamento",
            "Aprovar",
            "Expirar",
            "ColocarEmEspera",
            "Retomar",
            "CancelarPeloCliente",
            "Confirmar",
        };

        var data = new TheoryData<OrderStatus, string>();
        foreach (var origem in estadosE2)
        {
            foreach (var transicao in transicoes)
                data.Add(origem, transicao);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(TodosOsParesDeTransicao))]
    public void RN_COM_030_transicao_fora_da_tabela_falha_e_nao_altera_o_pedido(
        OrderStatus origem,
        string transicao
    )
    {
        var pedido = PedidoBuilder.Em(origem);
        var statusAntes = pedido.Status;

        var resultado = pedido.Executar(transicao);

        if (TabelaDeTransicoes.Permite(origem, transicao))
        {
            resultado.IsSuccess.Should().BeTrue($"esperava sucesso de {origem} via {transicao}");
        }
        else
        {
            resultado.IsFailure.Should().BeTrue($"esperava falha de {origem} via {transicao}");
            resultado.Error!.Value.Code.Should().Be("PEDIDO_TRANSICAO_INVALIDA");
            pedido.Status.Should().Be(statusAntes);
        }
    }
}

internal static class TabelaDeTransicoes
{
    private static readonly HashSet<(OrderStatus, string)> Permitidas =
    [
        (OrderStatus.Rascunho, "Enviar"),
        (OrderStatus.Enviado, "Analisar"),
        (OrderStatus.Enviado, "Recusar"),
        (OrderStatus.EmAnalise, "Recusar"),
        (OrderStatus.EmAnalise, "EnviarOrcamento"),
        (OrderStatus.OrcamentoEnviado, "RevisarOrcamento"),
        (OrderStatus.OrcamentoEnviado, "Aprovar"),
        (OrderStatus.OrcamentoEnviado, "Expirar"),
        (OrderStatus.OrcamentoEnviado, "ColocarEmEspera"),
        (OrderStatus.Aprovado, "ColocarEmEspera"),
        (OrderStatus.EmEspera, "Retomar"),
        (OrderStatus.Aprovado, "CancelarPeloCliente"),
        (OrderStatus.Aprovado, "Confirmar"),
    ];

    public static bool Permite(OrderStatus origem, string transicao) =>
        Permitidas.Contains((origem, transicao));
}
