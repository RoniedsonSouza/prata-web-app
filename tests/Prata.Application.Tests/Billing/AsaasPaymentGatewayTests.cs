using System.Net;
using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Prata.Application.Abstractions;
using Prata.Domain.Common;
using Prata.Infrastructure;
using Prata.Infrastructure.Payments;

namespace Prata.Application.Tests.Billing;

public class AsaasPaymentGatewayTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
    private static readonly Guid PaymentId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    [Fact]
    public void RN_FIN_020_assinatura_forjada_falha()
    {
        var gateway = CreateGateway(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var result = gateway.VerificarEAnalisar("{}", new Dictionary<string, string> { ["asaas-access-token"] = "forjado" });
        result.IsFailure.Should().BeTrue();
        result.Error!.Value.Code.Should().Be("WEBHOOK_ASSINATURA_INVALIDA");
    }

    [Fact]
    public void RN_FIN_020_webhook_asaas_extrai_charge_e_tenant()
    {
        var tenantId = TenantId;
        var paymentId = PaymentId;
        var xref = AsaasPaymentGateway.EncodeExternalReference(tenantId, paymentId);
        var raw =
            $"{{\"id\":\"evt_1\",\"event\":\"PAYMENT_RECEIVED\",\"payment\":{{\"id\":\"pay_abc\",\"externalReference\":\"{xref}\",\"status\":\"RECEIVED\",\"value\":100}}}}";

        var gateway = CreateGateway(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var result = gateway.VerificarEAnalisar(raw, new Dictionary<string, string> { ["asaas-access-token"] = "segredo-ok" });

        result.IsSuccess.Should().BeTrue();
        result.Value.ExternalEventId.Should().Be("evt_1");
        result.Value.EventType.Should().Be("PAYMENT_RECEIVED");
        result.Value.ExternalChargeId.Should().Be("pay_abc");
        result.Value.TenantId.Should().Be(tenantId);
        result.Value.PayloadJson.Should().Contain("chargeId");
        result.Value.PayloadJson.Should().Contain("pay_abc");
    }

    [Fact]
    public async Task RN_FIN_002_recusa_token_que_parece_PAN()
    {
        var gateway = CreateGateway(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var result = await gateway.CriarCobrancaAsync(
            new ChargeRequest(
                TenantId,
                PaymentId,
                Money.Brl(100m),
                "Cartao",
                8m,
                Money.Brl(8m),
                "wallet_foto",
                "key-1",
                CreditCardToken: "4111111111111111"
            ),
            CancellationToken.None
        );

        result.IsFailure.Should().BeTrue();
        result.Error!.Value.Code.Should().Be("CARTAO_PROIBIDO");
    }

    [Fact]
    public async Task CriarCobranca_envia_split_e_idempotency_com_HttpClient_mockado()
    {
        string? capturedBody = null;
        string? capturedIdempotency = null;
        var gateway = CreateGateway(req =>
        {
            var path = req.RequestUri?.AbsolutePath ?? "";
            if (path.Contains("customers", StringComparison.Ordinal) && req.Method == HttpMethod.Get)
            {
                return Json(new { data = Array.Empty<object>() });
            }

            if (path.Contains("customers", StringComparison.Ordinal) && req.Method == HttpMethod.Post)
            {
                return Json(new { id = "cus_1" });
            }

            if (path.Contains("payments", StringComparison.Ordinal) && req.Method == HttpMethod.Post)
            {
                capturedIdempotency = req.Headers.TryGetValues("Idempotency-Key", out var values) ? values.FirstOrDefault() : null;
                capturedBody = req.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
                return Json(
                    new
                    {
                        id = "pay_99",
                        status = "PENDING",
                        value = 100m,
                    }
                );
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var result = await gateway.CriarCobrancaAsync(
            new ChargeRequest(
                TenantId,
                PaymentId,
                Money.Brl(100m),
                "Pix",
                8m,
                Money.Brl(8m),
                "wallet_foto",
                "deposit:abc",
                CreditCardToken: null
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.ExternalChargeId.Should().Be("pay_99");
        capturedIdempotency.Should().Be("deposit:abc");
        capturedBody.Should().NotBeNullOrEmpty();
        capturedBody.Should().Contain("wallet_foto");
        capturedBody.Should().Contain("fixedValue");
        capturedBody.Should().Contain("PIX");
        capturedBody.Should().NotContain("411111");
    }

    [Fact]
    public void ShouldUseAsaas_exige_provider_e_chaves_reais()
    {
        DependencyInjection
            .ShouldUseAsaas(Config(("Payments:Provider", "Asaas"), ("Payments:Asaas:ApiKey", "CHANGE_ME")))
            .Should()
            .BeFalse();
        DependencyInjection
            .ShouldUseAsaas(
                Config(("Payments:Provider", "Asaas"), ("Payments:Asaas:ApiKey", "sk_test"), ("Payments:Asaas:WebhookSecret", "whsec"))
            )
            .Should()
            .BeTrue();
        DependencyInjection.ShouldUseAsaas(Config(("Payments:Provider", "Fake"), ("Payments:Asaas:ApiKey", "sk_test"))).Should().BeFalse();
    }

    private static AsaasPaymentGateway CreateGateway(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        var http = new HttpClient(new StubHandler(handler)) { BaseAddress = new Uri("https://api-sandbox.asaas.com/v3/") };
        var opts = Options.Create(
            new AsaasOptions
            {
                BaseUrl = "https://api-sandbox.asaas.com/v3",
                ApiKey = "sk_test",
                WebhookSecret = "segredo-ok",
                WalletIdPlatform = "wallet_plat",
            }
        );
        return new AsaasPaymentGateway(http, opts, new FixedClock(), NullLogger<AsaasPaymentGateway>.Instance);
    }

    private static HttpResponseMessage Json(object payload) =>
        new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json") };

    private static IConfiguration Config(params (string Key, string Value)[] pairs) =>
        new ConfigurationBuilder().AddInMemoryCollection(pairs.ToDictionary(p => p.Key, p => (string?)p.Value)).Build();

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(handler(request));
    }

    private sealed class FixedClock : IDateTimeProvider
    {
        public DateTimeOffset UtcNow => new(2026, 9, 12, 12, 0, 0, TimeSpan.Zero);
    }
}
