namespace Prata.Infrastructure.Payments;

/// <summary>Configuracao do PSP Asaas (ADR-0005). Segredos so via env / cofre — nunca commitados.</summary>
public sealed class AsaasOptions
{
    public const string SectionName = "Payments:Asaas";

    /// <summary>Sandbox: https://api-sandbox.asaas.com/v3 · Producao: https://api.asaas.com/v3</summary>
    public string BaseUrl { get; set; } = "https://api-sandbox.asaas.com/v3";

    public string ApiKey { get; set; } = "CHANGE_ME";

    /// <summary>Valor do header asaas-access-token no webhook (RN-FIN-020).</summary>
    public string WebhookSecret { get; set; } = "CHANGE_ME";

    /// <summary>walletId da conta da plataforma (comissao). Nao vai no split do fotografo.</summary>
    public string WalletIdPlatform { get; set; } = "CHANGE_ME";
}

/// <summary>Selecao do provedor de pagamento. Fake em dev/testes; Asaas quando configurado.</summary>
public sealed class PaymentsOptions
{
    public const string SectionName = "Payments";

    /// <summary>Fake | Asaas. Asaas so sobe se ApiKey != CHANGE_ME.</summary>
    public string Provider { get; set; } = "Fake";
}
