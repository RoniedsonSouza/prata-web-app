using AwesomeAssertions;
using Prata.Domain.Billing;
using Prata.Domain.Common;

namespace Prata.Domain.Tests.Billing;

public class MoneySplitTests
{
    [Theory]
    [InlineData(100, 3, "33.33", "33.33", "33.34")]
    [InlineData(10, 2, "5.00", "5.00")]
    [InlineData(1, 1, "1.00")]
    public void RN_FIN_004_diferenca_de_arredondamento_vai_na_ultima_parcela(
        decimal total,
        int parts,
        params string[] expected
    )
    {
        var money = Money.Brl(total);
        var parcels = MoneySplitter.SplitEvenly(money, parts);

        parcels.Should().HaveCount(parts);
        parcels
            .Select(p => p.Amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture))
            .Should()
            .Equal(expected);
        parcels.Aggregate(Money.Zero(), (acc, m) => acc.Add(m)).Should().Be(money);
    }
}

public class SplitRuleTests
{
    [Fact]
    public void RN_FIN_033_alterar_comissao_encerra_versao_e_cria_nova()
    {
        var agora = DateTimeOffset.Parse("2026-09-11T12:00:00Z");
        var atual = SplitRule.Create(tenantId: Guid.NewGuid(), percent: 8m, fixedAmount: null, vigenteDe: agora, createdBy: Guid.NewGuid());
        atual.IsSuccess.Should().BeTrue();

        var (encerrada, nova) = atual.Value.Substituir(novoPercent: 10m, agora: agora.AddDays(1), createdBy: Guid.NewGuid());
        encerrada.VigenteAte.Should().Be(agora.AddDays(1));
        nova.Percent.Should().Be(10m);
        nova.VigenteDe.Should().Be(agora.AddDays(1));
        nova.VigenteAte.Should().BeNull();
    }

    [Fact]
    public void RN_FIN_032_snapshot_preserva_comissao_original()
    {
        var agora = DateTimeOffset.UtcNow;
        var rule = SplitRule.Create(Guid.NewGuid(), 8m, null, agora, Guid.NewGuid()).Value;
        var snap = rule.CriarSnapshot();
        var (_, nova) = rule.Substituir(12m, agora.AddMinutes(1), Guid.NewGuid());

        snap.Percent.Should().Be(8m);
        nova.Percent.Should().Be(12m);
    }
}

public class DepositPercentTests
{
    [Theory]
    [InlineData(29)]
    [InlineData(51)]
    public void RN_FIN_010_sinal_fora_da_faixa_falha(decimal percent)
    {
        var result = DepositPolicy.ValidatePercent(percent);
        result.IsFailure.Should().BeTrue();
        result.Error!.Value.Code.Should().Be("SINAL_FORA_DA_FAIXA");
    }

    [Theory]
    [InlineData(30)]
    [InlineData(40)]
    [InlineData(50)]
    public void RN_FIN_010_sinal_na_faixa_passa(decimal percent)
    {
        DepositPolicy.ValidatePercent(percent).IsSuccess.Should().BeTrue();
    }
}
