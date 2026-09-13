using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Prata.Application.Abstractions;
using Prata.Domain.Common;

namespace Prata.Infrastructure.Payments;

/// <summary>
/// Adapter HTTP do Asaas (ADR-0005). Nunca aceita PAN/CVV — so token do PSP (RN-FIN-002).
/// </summary>
public sealed class AsaasPaymentGateway(
    HttpClient http,
    IOptions<AsaasOptions> options,
    IDateTimeProvider clock,
    ILogger<AsaasPaymentGateway> logger
) : IPaymentGateway
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly AsaasOptions _opts = options.Value;

    public async Task<Result<PayoutAccountRef>> CriarRecebedorAsync(RecebedorRequest request, CancellationToken cancellationToken)
    {
        var cpfCnpj = DigitsOnly(request.DocumentMasked);
        if (string.IsNullOrWhiteSpace(cpfCnpj) || cpfCnpj.Length is < 11 or > 14)
        {
            return Error.Validation(
                "DOCUMENTO_RECEBEDOR_INVALIDO",
                "Onboarding Asaas exige CPF/CNPJ numerico (nao mascarado). Conclua no painel do PSP se necessario."
            );
        }

        if (string.IsNullOrWhiteSpace(request.Email))
            return Error.Validation("EMAIL_RECEBEDOR_OBRIGATORIO", "Email do recebedor e obrigatorio no Asaas.");

        var body = new
        {
            name = request.Name,
            email = request.Email,
            cpfCnpj,
            birthDate = (string?)null,
            companyType = cpfCnpj.Length > 11 ? "LIMITED" : (string?)null,
        };

        using var response = await http.PostAsJsonAsync("accounts", body, JsonOptions, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return await FailAsync<PayoutAccountRef>(response, "ASAAS_CRIAR_RECEBEDOR", cancellationToken);

        var dto = await response.Content.ReadFromJsonAsync<AsaasAccountResponse>(JsonOptions, cancellationToken);
        if (dto is null || string.IsNullOrWhiteSpace(dto.WalletId))
            return Error.Validation("ASAAS_RESPOSTA_INVALIDA", "Asaas nao devolveu walletId.");

        // apiKey da subconta nunca e logada nem persistida aqui (RN-FIN-002 / segredos).
        return new PayoutAccountRef(dto.WalletId);
    }

    public async Task<Result<KycStatusDto>> ConsultarKycAsync(PayoutAccountRef reference, CancellationToken cancellationToken)
    {
        // Conta da plataforma consulta situacao comercial; walletId e o identificador guardado.
        using var response = await http.GetAsync($"accounts/{Uri.EscapeDataString(reference.ExternalRecipientId)}", cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // Alguns ambientes expoe so commercialInfo na conta autenticada.
            using var commercial = await http.GetAsync("myAccount/status/", cancellationToken);
            if (!commercial.IsSuccessStatusCode)
                return await FailAsync<KycStatusDto>(commercial, "ASAAS_KYC", cancellationToken);

            var statusDto = await commercial.Content.ReadFromJsonAsync<AsaasCommercialStatus>(JsonOptions, cancellationToken);
            return new KycStatusDto(MapKyc(statusDto?.General ?? statusDto?.CommercialInfo));
        }

        if (!response.IsSuccessStatusCode)
            return await FailAsync<KycStatusDto>(response, "ASAAS_KYC", cancellationToken);

        var account = await response.Content.ReadFromJsonAsync<AsaasAccountResponse>(JsonOptions, cancellationToken);
        return new KycStatusDto(MapKyc(account?.AccountStatus ?? account?.Status));
    }

    public async Task<Result<ChargeRef>> CriarCobrancaAsync(ChargeRequest request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.CreditCardToken) && LooksLikePan(request.CreditCardToken))
        {
            return Error.Validation("CARTAO_PROIBIDO", "Payload nao pode conter PAN/CVV (RN-FIN-002).");
        }

        var customer = await EnsureCustomerAsync(request.TenantId, cancellationToken);
        if (customer.IsFailure)
            return Result.Failure<ChargeRef>(customer.Error!.Value);

        var billingType = MapBillingType(request.Method);
        if (billingType is null)
            return Error.Validation("MEIO_PAGAMENTO_INVALIDO", $"Meio nao suportado no Asaas: {request.Method}.");

        var photographerShare = request.Amount.Amount - request.PlatformFeeAmount.Amount;
        if (photographerShare < 0)
            return Error.Validation("SPLIT_INVALIDO", "Comissao maior que o valor da cobranca.");

        var dueDate = DateOnly
            .FromDateTime(clock.UtcNow.UtcDateTime.Date)
            .AddDays(1)
            .ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        var externalRef = EncodeExternalReference(request.TenantId, request.PaymentId);

        var payload = new Dictionary<string, object?>
        {
            ["customer"] = customer.Value,
            ["billingType"] = billingType,
            ["value"] = request.Amount.Amount,
            ["dueDate"] = dueDate,
            ["externalReference"] = externalRef,
            ["description"] = $"Prata payment {request.PaymentId:N}",
        };

        if (
            !string.IsNullOrWhiteSpace(request.RecipientExternalId)
            && !request.RecipientExternalId.StartsWith("pending-", StringComparison.Ordinal)
            && photographerShare > 0
        )
        {
            payload["split"] = new[]
            {
                new Dictionary<string, object?>
                {
                    ["walletId"] = request.RecipientExternalId,
                    ["fixedValue"] = photographerShare,
                    ["externalReference"] = externalRef,
                },
            };
        }

        if (billingType == "CREDIT_CARD")
        {
            if (string.IsNullOrWhiteSpace(request.CreditCardToken))
                return Error.Validation("TOKEN_CARTAO_OBRIGATORIO", "Cartao exige token do PSP (RN-FIN-002).");
            payload["creditCardToken"] = request.CreditCardToken;
        }

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "payments")
        {
            Content = JsonContent.Create(payload, options: JsonOptions),
        };
        httpRequest.Headers.TryAddWithoutValidation("Idempotency-Key", request.IdempotencyKey);

        using var response = await http.SendAsync(httpRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return await FailAsync<ChargeRef>(response, "ASAAS_CRIAR_COBRANCA", cancellationToken);

        var dto = await response.Content.ReadFromJsonAsync<AsaasPaymentResponse>(JsonOptions, cancellationToken);
        if (dto is null || string.IsNullOrWhiteSpace(dto.Id))
            return Error.Validation("ASAAS_RESPOSTA_INVALIDA", "Asaas nao devolveu id da cobranca.");

        return new ChargeRef(dto.Id);
    }

    public async Task<Result<ChargeState>> ConsultarCobrancaAsync(ChargeRef reference, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync($"payments/{Uri.EscapeDataString(reference.ExternalChargeId)}", cancellationToken);
        if (!response.IsSuccessStatusCode)
            return await FailAsync<ChargeState>(response, "ASAAS_CONSULTAR_COBRANCA", cancellationToken);

        var dto = await response.Content.ReadFromJsonAsync<AsaasPaymentResponse>(JsonOptions, cancellationToken);
        if (dto is null)
            return Error.Validation("ASAAS_RESPOSTA_INVALIDA", "Resposta vazia ao consultar cobranca.");

        return new ChargeState(MapChargeStatus(dto.Status), ParseConfirmedAt(dto));
    }

    public async Task<Result<Unit>> CancelarCobrancaAsync(ChargeRef reference, CancellationToken cancellationToken)
    {
        using var response = await http.DeleteAsync($"payments/{Uri.EscapeDataString(reference.ExternalChargeId)}", cancellationToken);
        if (!response.IsSuccessStatusCode)
            return await FailAsync<Unit>(response, "ASAAS_CANCELAR_COBRANCA", cancellationToken);

        return Unit.Value;
    }

    public async Task<Result<RefundRef>> EstornarAsync(ChargeRef reference, Money? parcial, CancellationToken cancellationToken)
    {
        object body = parcial is null ? new { } : new { value = parcial.Value.Amount };
        using var response = await http.PostAsJsonAsync(
            $"payments/{Uri.EscapeDataString(reference.ExternalChargeId)}/refund",
            body,
            JsonOptions,
            cancellationToken
        );
        if (!response.IsSuccessStatusCode)
            return await FailAsync<RefundRef>(response, "ASAAS_ESTORNAR", cancellationToken);

        var dto = await response.Content.ReadFromJsonAsync<AsaasRefundResponse>(JsonOptions, cancellationToken);
        var id = dto?.Id ?? $"ref_{reference.ExternalChargeId}";
        return new RefundRef(id);
    }

    public Result<WebhookEnvelope> VerificarEAnalisar(string rawBody, IReadOnlyDictionary<string, string> headers)
    {
        if (
            !headers.TryGetValue("asaas-access-token", out var token)
            || string.IsNullOrWhiteSpace(_opts.WebhookSecret)
            || _opts.WebhookSecret == "CHANGE_ME"
            || !string.Equals(token, _opts.WebhookSecret, StringComparison.Ordinal)
        )
        {
            return Error.Validation("WEBHOOK_ASSINATURA_INVALIDA", "Assinatura de webhook invalida (RN-FIN-020).");
        }

        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(rawBody) ? "{}" : rawBody);
            var root = doc.RootElement;
            var eventType = root.TryGetProperty("event", out var ev) ? ev.GetString() ?? "" : "";
            var eventId = root.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.String ? idEl.GetString()! : null;

            string? chargeId = null;
            string? externalRef = null;
            if (root.TryGetProperty("payment", out var payment) && payment.ValueKind == JsonValueKind.Object)
            {
                if (payment.TryGetProperty("id", out var payId))
                    chargeId = payId.GetString();
                if (payment.TryGetProperty("externalReference", out var xref))
                    externalRef = xref.GetString();
            }

            if (string.IsNullOrWhiteSpace(eventId))
            {
                // Asaas às vezes omite id do envelope; compõe chave estável (RN-FIN-021).
                eventId = $"{eventType}:{chargeId}:{externalRef}";
            }

            if (string.IsNullOrWhiteSpace(eventType))
                return Error.Validation("WEBHOOK_PAYLOAD_INVALIDO", "Evento Asaas sem campo event.");

            Guid? tenantId = null;
            if (!string.IsNullOrWhiteSpace(externalRef) && TryParseExternalReference(externalRef, out var tid, out _))
                tenantId = tid;

            var payload = BuildWebhookPayload(chargeId, rawBody);
            return new WebhookEnvelope(eventId, eventType, tenantId, chargeId, payload);
        }
        catch (JsonException)
        {
            return Error.Validation("WEBHOOK_PAYLOAD_INVALIDO", "JSON de webhook invalido.");
        }
    }

    public async Task<Result<IReadOnlyList<SettlementLine>>> ObterExtratoAsync(
        DateOnly de,
        DateOnly ate,
        CancellationToken cancellationToken
    )
    {
        var url = $"payments?dateCreated[ge]={de:yyyy-MM-dd}&dateCreated[le]={ate:yyyy-MM-dd}&limit=100";
        using var response = await http.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return await FailAsync<IReadOnlyList<SettlementLine>>(response, "ASAAS_EXTRATO", cancellationToken);

        var list = await response.Content.ReadFromJsonAsync<AsaasListResponse<AsaasPaymentResponse>>(JsonOptions, cancellationToken);
        var lines = new List<SettlementLine>();
        if (list?.Data is not null)
        {
            foreach (var p in list.Data)
            {
                if (string.IsNullOrWhiteSpace(p.Id))
                    continue;
                var date = DateOnly.TryParse(
                    p.DateCreated?[..Math.Min(10, p.DateCreated.Length)],
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None,
                    out var d
                )
                    ? d
                    : de;
                lines.Add(new SettlementLine(p.Id, Money.Brl(p.Value), MapChargeStatus(p.Status), date));
            }
        }

        IReadOnlyList<SettlementLine> result = lines;
        return Result.Success(result);
    }

    private async Task<Result<string>> EnsureCustomerAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var externalRef = tenantId.ToString("N");
        using (
            var listResponse = await http.GetAsync(
                $"customers?externalReference={Uri.EscapeDataString(externalRef)}&limit=1",
                cancellationToken
            )
        )
        {
            if (listResponse.IsSuccessStatusCode)
            {
                var existing = await listResponse.Content.ReadFromJsonAsync<AsaasListResponse<AsaasCustomerResponse>>(
                    JsonOptions,
                    cancellationToken
                );
                var id = existing?.Data?.FirstOrDefault()?.Id;
                if (!string.IsNullOrWhiteSpace(id))
                    return id;
            }
        }

        var body = new
        {
            name = $"Prata tenant {externalRef[..8]}",
            email = $"tenant-{externalRef}@clientes.prata.app",
            externalReference = externalRef,
            notificationDisabled = true,
        };

        using var create = await http.PostAsJsonAsync("customers", body, JsonOptions, cancellationToken);
        if (!create.IsSuccessStatusCode)
            return await FailAsync<string>(create, "ASAAS_CRIAR_CLIENTE", cancellationToken);

        var created = await create.Content.ReadFromJsonAsync<AsaasCustomerResponse>(JsonOptions, cancellationToken);
        if (created is null || string.IsNullOrWhiteSpace(created.Id))
            return Error.Validation("ASAAS_RESPOSTA_INVALIDA", "Asaas nao devolveu id do cliente.");

        return created.Id;
    }

    private async Task<Result<T>> FailAsync<T>(HttpResponseMessage response, string code, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        logger.LogWarning("Asaas HTTP {Status} code={Code} bodyLength={Length}", (int)response.StatusCode, code, body.Length);
        // Nao ecoa corpo (pode conter dado sensivel).
        return Error.Validation(code, $"Falha no Asaas HTTP {(int)response.StatusCode}.");
    }

    public static string EncodeExternalReference(Guid tenantId, Guid paymentId) => $"{tenantId:N}:{paymentId:N}";

    public static bool TryParseExternalReference(string value, out Guid tenantId, out Guid paymentId)
    {
        tenantId = default;
        paymentId = default;
        var parts = value.Split(':', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
            return false;
        return Guid.TryParseExact(parts[0], "N", out tenantId) && Guid.TryParseExact(parts[1], "N", out paymentId);
    }

    private static string BuildWebhookPayload(string? chargeId, string rawBody)
    {
        if (string.IsNullOrWhiteSpace(chargeId))
            return rawBody;

        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(rawBody) ? "{}" : rawBody);
            if (doc.RootElement.TryGetProperty("chargeId", out _))
                return rawBody;

            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                writer.WriteString("chargeId", chargeId);
                writer.WritePropertyName("raw");
                doc.RootElement.WriteTo(writer);
                writer.WriteEndObject();
            }

            return Encoding.UTF8.GetString(stream.ToArray());
        }
        catch (JsonException)
        {
            return $"{{\"chargeId\":\"{chargeId}\"}}";
        }
    }

    private static string? MapBillingType(string method) =>
        method.ToUpperInvariant() switch
        {
            "PIX" => "PIX",
            "BOLETO" => "BOLETO",
            "CARTAO" or "CREDIT_CARD" or "CREDITCARD" => "CREDIT_CARD",
            _ => null,
        };

    private static string MapChargeStatus(string? status) =>
        status?.ToUpperInvariant() switch
        {
            "PENDING" or "AWAITING_PAYMENT" or "AWAITING_RISK_ANALYSIS" or "OVERDUE" => "Pendente",
            "RECEIVED" or "CONFIRMED" or "RECEIVED_IN_CASH" or "DUNNING_RECEIVED" => "Confirmado",
            "REFUNDED" or "REFUND_REQUESTED" or "CHARGEBACK_REQUESTED" or "CHARGEBACK_DISPUTE" => "Estornado",
            "DELETED" or "CANCELLED" or "CANCELED" => "Cancelado",
            _ => status ?? "Pendente",
        };

    private static string MapKyc(string? status) =>
        status?.ToUpperInvariant() switch
        {
            "APPROVED" or "APROVADO" or "ENABLED" or "ACTIVE" => "Aprovado",
            "REJECTED" or "REPROVADO" or "DISABLED" => "Reprovado",
            "PENDING" or "AWAITING_APPROVAL" or "IN_REVIEW" => "Pendente",
            _ => "Pendente",
        };

    private static DateTimeOffset? ParseConfirmedAt(AsaasPaymentResponse dto)
    {
        if (string.IsNullOrWhiteSpace(dto.ConfirmedDate) && string.IsNullOrWhiteSpace(dto.PaymentDate))
            return null;
        var raw = dto.ConfirmedDate ?? dto.PaymentDate;
        return DateTimeOffset.TryParse(raw, out var at) ? at : null;
    }

    private static string DigitsOnly(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";
        return new string(value.Where(char.IsDigit).ToArray());
    }

    private static bool LooksLikePan(string token) => token.All(char.IsDigit) && token.Length is >= 13 and <= 19;

    private sealed class AsaasAccountResponse
    {
        public string? Id { get; set; }
        public string? WalletId { get; set; }
        public string? AccountStatus { get; set; }
        public string? Status { get; set; }
    }

    private sealed class AsaasCommercialStatus
    {
        public string? General { get; set; }
        public string? CommercialInfo { get; set; }
    }

    private sealed class AsaasCustomerResponse
    {
        public string? Id { get; set; }
    }

    private sealed class AsaasPaymentResponse
    {
        public string? Id { get; set; }
        public string? Status { get; set; }
        public decimal Value { get; set; }
        public string? DateCreated { get; set; }
        public string? ConfirmedDate { get; set; }
        public string? PaymentDate { get; set; }
        public string? ExternalReference { get; set; }
    }

    private sealed class AsaasRefundResponse
    {
        public string? Id { get; set; }
    }

    private sealed class AsaasListResponse<T>
    {
        public List<T>? Data { get; set; }
    }
}
