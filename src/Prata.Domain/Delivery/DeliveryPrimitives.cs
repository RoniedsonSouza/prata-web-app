using Prata.Domain.Common;

namespace Prata.Domain.Delivery;

public static class DeliveryErrors
{
    public static readonly Error DerivadasPendentes = new(
        "GALERIA_DERIVADAS_PENDENTES",
        "Todas as fotos precisam de thumb e web (RN-ENT-010)."
    );

    public static readonly Error TransicaoInvalida = new(
        "GALERIA_TRANSICAO_INVALIDA",
        "Transicao de galeria invalida."
    );

    public static readonly Error SelecaoExcede = new(
        "SELECAO_EXCEDE_LIMITE",
        "Selecao acima do limite exige upsell confirmado (RN-ENT-020)."
    );

    public static readonly Error ShareSenhaCurta = new(
        "SHARE_SENHA_CURTA",
        "Senha do ShareLink deve ter no minimo 6 caracteres (RN-ENT-040)."
    );

    public static readonly Error FotoHashDuplicado = new(
        "FOTO_HASH_DUPLICADO",
        "Ja existe foto com este hash na galeria."
    );
}

public enum GalleryStatus
{
    EmPreparo = 0,
    Disponivel = 1,
    EmSelecao = 2,
    SelecaoFechada = 3,
    Entregue = 4,
    BloqueadaPorPendencia = 5,
    Expirada = 6,
    Arquivada = 7,
}

public enum PortfolioConsent
{
    Sim = 0,
    SomenteSemRosto = 1,
    Nao = 2,
}

public enum PhotoVariantKind
{
    Original = 0,
    Thumb = 1,
    Web = 2,
    Texture = 3,
    Lqip = 4,
}

public enum DownloadJobStatus
{
    Pendente = 0,
    Processando = 1,
    Pronto = 2,
    Falhou = 3,
    Expirado = 4,
}

/// <summary>Contexto externo das guardas (derivadas, saldo, upsell).</summary>
public sealed record GalleryExecContext(
    bool TodasDerivadasProntas,
    bool UpsellConfirmado,
    bool SaldoConfirmado,
    bool PeloJob
)
{
    public static GalleryExecContext Ok { get; } = new(true, true, true, false);
}
