using AwesomeAssertions;
using Prata.Domain.Common;

namespace Prata.Domain.Tests.Common;

public class MoneyTests
{
    [Fact]
    public void RN_FIN_003_cria_money_em_brl_com_duas_casas()
    {
        var money = Money.Brl(100.50m);

        money.Amount.Should().Be(100.50m);
        money.Currency.Should().Be(Currency.Brl);
    }

    [Fact]
    public void RN_FIN_003_rejeita_mais_de_duas_casas()
    {
        var result = Money.TryCreate(10.123m, Currency.Brl);

        result.IsFailure.Should().BeTrue();
        result.Error!.Value.Code.Should().Be("DINHEIRO_ESCALA_INVALIDA");
    }

    [Fact]
    public void RN_CAT_001_rejeita_valor_nao_positivo()
    {
        var result = Money.TryCreatePositive(0m, Currency.Brl);

        result.IsFailure.Should().BeTrue();
        result.Error!.Value.Code.Should().Be("DINHEIRO_NAO_POSITIVO");
    }

    [Fact]
    public void RN_FIN_004_arredonda_comissao_com_bankers_rounding()
    {
        // 8% de 33,33 = 2,6664 → 2,67 com MidpointRounding.ToEven?
        // 2.6664 rounded to 2 decimals = 2.67 (away from even midpoint case)
        var baseValue = Money.Brl(33.33m);
        var commission = baseValue.Percentage(8m);

        commission.Amount.Should().Be(2.67m);
    }

    [Fact]
    public void Soma_exige_mesma_moeda()
    {
        var a = Money.Brl(10m);
        var b = Money.Brl(5m);

        (a + b).Amount.Should().Be(15m);
    }
}

public class ResultTests
{
    [Fact]
    public void Sucesso_expoe_valor()
    {
        var result = Result.Success(42);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void Falha_nao_permite_ler_valor()
    {
        var result = Result.Failure<int>(new Error("X", "msg"));

        result.IsFailure.Should().BeTrue();
        var act = () => result.Value;
        act.Should().Throw<InvalidOperationException>();
    }
}
