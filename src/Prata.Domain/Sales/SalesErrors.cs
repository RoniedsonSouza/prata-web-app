using Prata.Domain.Common;

namespace Prata.Domain.Sales;

public static class SalesErrors
{
    public static readonly Error TenantInvalido = new("PEDIDO_TENANT_INVALIDO", "TenantId e obrigatorio.");

    public static readonly Error ClienteNomeObrigatorio = new(
        "CLIENTE_NOME_OBRIGATORIO",
        "Nome do cliente e obrigatorio."
    );

    public static readonly Error ClienteEmailInvalido = new(
        "CLIENTE_EMAIL_INVALIDO",
        "E-mail do cliente e invalido."
    );

    public static readonly Error PedidoDataPassado = new(
        "PEDIDO_DATA_PASSADO",
        "Data pretendida nao pode estar no passado."
    );

    public static readonly Error PedidoTotalBloqueado = new(
        "PEDIDO_TOTAL_BLOQUEADO",
        "Total so pode ser recalculado ate OrcamentoEnviado."
    );

    public static readonly Error PedidoDescontoExcede = new(
        "PEDIDO_DESCONTO_EXCEDE_SUBTOTAL",
        "Desconto nao pode exceder o subtotal."
    );

    public static readonly Error DescontoInvalido = new(
        "PEDIDO_DESCONTO_INVALIDO",
        "Desconto deve ser positivo; percentual entre 0 e 100."
    );

    public static readonly Error ItemCatalogoInvalido = new(
        "PEDIDO_ITEM_CATALOGO_INVALIDO",
        "Item de catalogo e obrigatorio."
    );

    public static readonly Error ItemNomeObrigatorio = new(
        "PEDIDO_ITEM_NOME_OBRIGATORIO",
        "Snapshot de nome do item e obrigatorio."
    );

    public static readonly Error ItemPrecoInvalido = new(
        "PEDIDO_ITEM_PRECO_INVALIDO",
        "Preco do item nao pode ser negativo."
    );

    public static readonly Error ItemQuantidadeInvalida = new(
        "PEDIDO_ITEM_QUANTIDADE_INVALIDA",
        "Quantidade do item deve ser maior que zero."
    );

    public static readonly Error OrcamentoVersaoInvalida = new(
        "ORCAMENTO_VERSAO_INVALIDA",
        "Versao do orcamento deve ser >= 1."
    );

    public static readonly Error OrcamentoValidadeInvalida = new(
        "ORCAMENTO_VALIDADE_INVALIDA",
        "Validade do orcamento deve ser de pelo menos 1 dia."
    );

    public static readonly Error OrcamentoTotalInvalido = new(
        "ORCAMENTO_TOTAL_INVALIDO",
        "Orcamento exige total maior que zero."
    );

    public static readonly Error OrcamentoSemItens = new(
        "ORCAMENTO_SEM_ITENS",
        "Orcamento exige pelo menos um item."
    );

    public static readonly Error OrcamentoExpirado = new(
        "ORCAMENTO_EXPIRADO",
        "Orcamento fora da validade."
    );

    public static readonly Error OrcamentoAindaVigente = new(
        "ORCAMENTO_AINDA_VIGENTE",
        "Orcamento ainda esta dentro da validade; nao pode expirar."
    );

    public static readonly Error MotivoObrigatorio = new(
        "PEDIDO_MOTIVO_OBRIGATORIO",
        "Motivo e obrigatorio nesta transicao."
    );

    public static readonly Error BriefingIncompleto = new(
        "PEDIDO_BRIEFING_INCOMPLETO",
        "Existem perguntas obrigatorias pendentes no briefing."
    );

    public static readonly Error ConfirmacaoIncompleta = new(
        "PEDIDO_CONFIRMACAO_INCOMPLETA",
        "Confirmacao exige sinal Confirmado e contrato Assinado."
    );

    public static Error TransicaoInvalida(OrderStatus de, string para) =>
        new(
            "PEDIDO_TRANSICAO_INVALIDA",
            $"Transicao invalida de {de} para {para}."
        );
}
