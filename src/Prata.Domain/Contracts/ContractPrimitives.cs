using Prata.Domain.Common;

namespace Prata.Domain.Contracts;

public enum ContractStatus
{
    Rascunho = 0,
    Enviado = 1,
    Visualizado = 2,
    Assinado = 3,
    Recusado = 4,
    Expirado = 5,
    Cancelado = 6,
}

/// <summary>Clausulas obrigatorias do PDF (RN-CTR-002).</summary>
public static class ContractRequiredClauses
{
    public const string GaleriaExpiracao = "CL_GALERIA_EXPIRACAO";
    public const string ConsentimentoImagem = "CL_CONSENTIMENTO_IMAGEM";
    public const string ConsentimentoMenor = "CL_CONSENTIMENTO_MENOR";

    public static readonly IReadOnlyList<string> All =
    [
        GaleriaExpiracao,
        ConsentimentoImagem,
        ConsentimentoMenor,
    ];
}

public static class ContractErrors
{
    public static readonly Error ClausulasObrigatorias = new(
        "CONTRATO_CLAUSULAS_OBRIGATORIAS",
        "Contrato precisa das clausulas de expiracao da galeria, consentimento de imagem e consentimento do responsavel por menor."
    );

    public static readonly Error HashObrigatorio = new(
        "CONTRATO_HASH_OBRIGATORIO",
        "PDF precisa de hash SHA-256 antes do envio (RN-CTR-001)."
    );

    public static readonly Error AssinadoImutavel = new(
        "CONTRATO_ASSINADO_IMUTAVEL",
        "Contrato assinado e imutavel; gere uma correcao (RN-CTR-011)."
    );

    public static readonly Error Expirado = new(
        "CONTRATO_EXPIRADO",
        "Contrato expirado nao pode ser assinado (RN-CTR-030)."
    );

    public static readonly Error AssinaturaIncompleta = new(
        "CONTRATO_ASSINATURA_INCOMPLETA",
        "Assinatura exige hash, IP, user-agent e timestamp (RN-CTR-010)."
    );

    public static readonly Error HashDivergente = new(
        "CONTRATO_HASH_DIVERGENTE",
        "Hash do aceite diverge do PDF enviado."
    );

    public static Error TransicaoInvalida(ContractStatus de, string para) =>
        new("CONTRATO_TRANSICAO_INVALIDA", $"Transicao invalida de {de} para {para}.");
}
