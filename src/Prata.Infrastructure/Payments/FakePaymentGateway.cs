using Prata.Application.Abstractions;
using Prata.Domain.Common;

namespace Prata.Infrastructure.Payments;

/// <summary>Gateway em memoria para testes — nenhum HTTP real (docs/08 §9).</summary>
public sealed class FakePaymentGateway : IPaymentGateway
{
    private readonly Dictionary<string, ChargeState> _charges = new(StringComparer.Ordinal);
    private readonly HashSet<string> _seenEvents = new(StringComparer.Ordinal);

    public Task<Result<PayoutAccountRef>> CriarRecebedorAsync(
        RecebedorRequest request,
        CancellationToken cancellationToken
    )
    {
        _ = cancellationToken;
        var id = $"recv_{request.TenantId:N}";
        return Task.FromResult(Result.Success(new PayoutAccountRef(id)));
    }

    public Task<Result<KycStatusDto>> ConsultarKycAsync(
        PayoutAccountRef reference,
        CancellationToken cancellationToken
    )
    {
        _ = reference;
        _ = cancellationToken;
        return Task.FromResult(Result.Success(new KycStatusDto("Pendente")));
    }

    public Task<Result<ChargeRef>> CriarCobrancaAsync(ChargeRequest request, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        if (!string.IsNullOrWhiteSpace(request.CreditCardToken) && LooksLikePan(request.CreditCardToken))
            return Task.FromResult(
                Result.Failure<ChargeRef>(
                    Error.Validation("CARTAO_PROIBIDO", "Payload nao pode conter PAN/CVV (RN-FIN-002).")
                )
            );

        var id = $"chg_{request.PaymentId:N}";
        _charges[id] = new ChargeState("Pendente", null);
        return Task.FromResult(Result.Success(new ChargeRef(id)));
    }

    public Task<Result<ChargeState>> ConsultarCobrancaAsync(
        ChargeRef reference,
        CancellationToken cancellationToken
    )
    {
        _ = cancellationToken;
        if (!_charges.TryGetValue(reference.ExternalChargeId, out var state))
            return Task.FromResult(
                Result.Failure<ChargeState>(Error.Validation("COBRANCA_NAO_ENCONTRADA", "Cobranca ausente no fake."))
            );
        return Task.FromResult(Result.Success(state));
    }

    public Task<Result<Unit>> CancelarCobrancaAsync(ChargeRef reference, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        _charges[reference.ExternalChargeId] = new ChargeState("Cancelado", null);
        return Task.FromResult(Result.Success(Unit.Value));
    }

    public Task<Result<RefundRef>> EstornarAsync(
        ChargeRef reference,
        Money? parcial,
        CancellationToken cancellationToken
    )
    {
        _ = reference;
        _ = parcial;
        _ = cancellationToken;
        return Task.FromResult(Result.Success(new RefundRef($"ref_{Guid.NewGuid():N}")));
    }

    public Result<WebhookEnvelope> VerificarEAnalisar(
        string rawBody,
        IReadOnlyDictionary<string, string> headers
    )
    {
        if (!headers.TryGetValue("asaas-access-token", out var token) || token != "sandbox-ok")
            return Error.Validation("WEBHOOK_ASSINATURA_INVALIDA", "Assinatura de webhook invalida (RN-FIN-020).");

        // Formato minimo: externalEventId|eventType|tenantId|chargeId|payload
        var parts = rawBody.Split('|', 5);
        if (parts.Length < 5)
            return Error.Validation("WEBHOOK_PAYLOAD_INVALIDO", "Payload de webhook invalido.");

        var envelope = new WebhookEnvelope(parts[0], parts[1], Guid.Parse(parts[2]), parts[3], parts[4]);
        if (!_seenEvents.Add(envelope.ExternalEventId))
        {
            // Idempotencia e responsabilidade da Application; fake so analisa.
        }

        return envelope;
    }

    public Task<Result<IReadOnlyList<SettlementLine>>> ObterExtratoAsync(
        DateOnly de,
        DateOnly ate,
        CancellationToken cancellationToken
    )
    {
        _ = de;
        _ = ate;
        _ = cancellationToken;
        IReadOnlyList<SettlementLine> empty = Array.Empty<SettlementLine>();
        return Task.FromResult(Result.Success(empty));
    }

    public void SimularConfirmacao(string chargeId, DateTimeOffset at) =>
        _charges[chargeId] = new ChargeState("Confirmado", at);

    private static bool LooksLikePan(string token) =>
        token.All(char.IsDigit) && token.Length is >= 13 and <= 19;
}
