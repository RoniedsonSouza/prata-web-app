using AwesomeAssertions;
using Prata.Application.Abstractions;
using Prata.Domain.Common;
using Prata.Infrastructure.Payments;

namespace Prata.Application.Tests.Billing;

public class FakePaymentGatewayTests
{
    [Fact]
    public async Task RN_FIN_002_recusa_token_que_parece_PAN()
    {
        var gateway = new FakePaymentGateway();
        var result = await gateway.CriarCobrancaAsync(
            new ChargeRequest(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Money.Brl(100m),
                "Cartao",
                8m,
                Money.Brl(8m),
                "recv_1",
                "key-1",
                CreditCardToken: "4111111111111111"
            ),
            CancellationToken.None
        );

        result.IsFailure.Should().BeTrue();
        result.Error!.Value.Code.Should().Be("CARTAO_PROIBIDO");
    }

    [Fact]
    public void RN_FIN_020_assinatura_forjada_falha()
    {
        var gateway = new FakePaymentGateway();
        var result = gateway.VerificarEAnalisar(
            "evt|PAYMENT_RECEIVED|00000000-0000-0000-0000-000000000001|chg|{}",
            new Dictionary<string, string> { ["asaas-access-token"] = "forjado" }
        );
        result.IsFailure.Should().BeTrue();
        result.Error!.Value.Code.Should().Be("WEBHOOK_ASSINATURA_INVALIDA");
    }

    [Fact]
    public void RN_FIN_020_payload_inclui_chargeId()
    {
        var gateway = new FakePaymentGateway();
        var tenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var result = gateway.VerificarEAnalisar(
            $"evt-1|PAYMENT_RECEIVED|{tenantId:D}|chg_abc|{{}}",
            new Dictionary<string, string> { ["asaas-access-token"] = "sandbox-ok" }
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.ExternalChargeId.Should().Be("chg_abc");
        result.Value.PayloadJson.Should().Contain("chargeId");
        result.Value.PayloadJson.Should().Contain("chg_abc");
    }
}
