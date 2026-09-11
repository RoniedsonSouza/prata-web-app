using AwesomeAssertions;
using Prata.Domain.Common;
using Prata.Domain.Sales;

namespace Prata.Domain.Tests.Sales;

public class OrderTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ClientId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ServiceTypeId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly DateTimeOffset Agora = new(2026, 9, 11, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void RN_COM_010_rejeita_data_pretendida_no_passado()
    {
        var ontem = DateOnly.FromDateTime(Agora.UtcDateTime.Date.AddDays(-1));

        var result = Order.Create(TenantId, ClientId, ServiceTypeId, ontem, Agora);

        result.IsFailure.Should().BeTrue();
        result.Error!.Value.Code.Should().Be("PEDIDO_DATA_PASSADO");
    }

    [Fact]
    public void RN_COM_001_recalcula_total_ate_orcamento_enviado()
    {
        var pedido = CriarRascunhoVazio();

        pedido
            .AdicionarItem(OrderItemKind.Package, Guid.NewGuid(), "Pacote Base", Money.Brl(1000m), quantity: 1)
            .IsSuccess.Should()
            .BeTrue();
        pedido.AplicarDesconto(Discount.Fixed(Money.Brl(100m))).IsSuccess.Should().BeTrue();

        pedido.Subtotal.Amount.Should().Be(1000m);
        pedido.Total.Amount.Should().Be(900m);
    }

    [Fact]
    public void RN_COM_001_nao_recalcula_apos_aprovado()
    {
        var pedido = PedidoBuilder.Em(OrderStatus.Aprovado);
        var totalAntes = pedido.Total;

        var add = pedido.AdicionarItem(
            OrderItemKind.Addon,
            Guid.NewGuid(),
            "Extra",
            Money.Brl(50m),
            quantity: 1
        );

        add.IsFailure.Should().BeTrue();
        add.Error!.Value.Code.Should().Be("PEDIDO_TOTAL_BLOQUEADO");
        pedido.Total.Should().Be(totalAntes);
    }

    [Fact]
    public void RN_COM_002_desconto_nao_excede_subtotal()
    {
        var pedido = CriarRascunhoVazio();
        pedido
            .AdicionarItem(OrderItemKind.Package, Guid.NewGuid(), "Pacote", Money.Brl(100m), 1)
            .IsSuccess.Should()
            .BeTrue();

        var result = pedido.AplicarDesconto(Discount.Fixed(Money.Brl(150m)));

        result.IsFailure.Should().BeTrue();
        result.Error!.Value.Code.Should().Be("PEDIDO_DESCONTO_EXCEDE_SUBTOTAL");
    }

    [Fact]
    public void RN_COM_013_valido_ate_e_gravado_no_quote()
    {
        var pedido = PedidoBuilder.Em(OrderStatus.EmAnalise);
        var enviadoEm = Agora;
        var validadeDias = 10;

        pedido.EnviarOrcamento(enviadoEm, validadeDias).IsSuccess.Should().BeTrue();

        pedido.Status.Should().Be(OrderStatus.OrcamentoEnviado);
        pedido.CurrentQuote.Should().NotBeNull();
        pedido.CurrentQuote!.ValidoAte.Should().Be(enviadoEm.AddDays(validadeDias));
        pedido.CurrentQuote.Version.Should().Be(1);
    }

    [Fact]
    public void RN_COM_022_revisar_orcamento_reinicia_validade_e_incrementa_versao()
    {
        var pedido = PedidoBuilder.Em(OrderStatus.OrcamentoEnviado);
        var primeiraValidade = pedido.CurrentQuote!.ValidoAte;
        var revisaoEm = Agora.AddDays(2);

        pedido.RevisarOrcamento(revisaoEm, validadeDias: 7).IsSuccess.Should().BeTrue();

        pedido.CurrentQuote!.Version.Should().Be(2);
        pedido.CurrentQuote.ValidoAte.Should().Be(revisaoEm.AddDays(7));
        pedido.CurrentQuote.ValidoAte.Should().NotBe(primeiraValidade);
        pedido.Quotes.Should().HaveCount(2);
    }

    [Fact]
    public void RN_COM_031_em_espera_guarda_origem_e_retoma()
    {
        var pedido = PedidoBuilder.Em(OrderStatus.OrcamentoEnviado);

        pedido.ColocarEmEspera("cliente pensando").IsSuccess.Should().BeTrue();
        pedido.Status.Should().Be(OrderStatus.EmEspera);
        pedido.StatusAntesDaEspera.Should().Be(OrderStatus.OrcamentoEnviado);

        pedido.Retomar().IsSuccess.Should().BeTrue();
        pedido.Status.Should().Be(OrderStatus.OrcamentoEnviado);
        pedido.StatusAntesDaEspera.Should().BeNull();
    }

    [Fact]
    public void RN_COM_020_nao_confirma_sem_sinal_e_contrato()
    {
        var pedido = PedidoBuilder.Em(OrderStatus.Aprovado);

        var semSinal = pedido.Confirmar(sinalConfirmado: false, contratoAssinado: true, Agora);
        semSinal.IsFailure.Should().BeTrue();
        semSinal.Error!.Value.Code.Should().Be("PEDIDO_CONFIRMACAO_INCOMPLETA");

        var semContrato = pedido.Confirmar(sinalConfirmado: true, contratoAssinado: false, Agora);
        semContrato.IsFailure.Should().BeTrue();
        semContrato.Error!.Value.Code.Should().Be("PEDIDO_CONFIRMACAO_INCOMPLETA");

        pedido.Status.Should().Be(OrderStatus.Aprovado);
    }

    [Fact]
    public void RN_CAT_004_item_guarda_snapshot_de_nome_e_preco()
    {
        var pedido = CriarRascunhoVazio();
        var packageId = Guid.NewGuid();

        pedido
            .AdicionarItem(OrderItemKind.Package, packageId, "Ensaio Casal", Money.Brl(1800m), 1)
            .IsSuccess.Should()
            .BeTrue();

        var item = pedido.Items.Single();
        item.CatalogItemId.Should().Be(packageId);
        item.NameSnapshot.Should().Be("Ensaio Casal");
        item.UnitPriceSnapshot.Amount.Should().Be(1800m);
    }

    [Fact]
    public void RN_AGD_030_rejeita_data_ja_confirmada_no_tenant()
    {
        var data = new DateOnly(2026, 10, 1);
        var resultado = Order.VerificarConflitoDeData(data, [data]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Value.Code.Should().Be("PEDIDO_DATA_CONFLITO");
    }

    private static Order CriarRascunhoVazio()
    {
        var dataPretendida = DateOnly.FromDateTime(Agora.UtcDateTime.Date.AddDays(30));
        return Order.Create(TenantId, ClientId, ServiceTypeId, dataPretendida, Agora).Value;
    }
}
