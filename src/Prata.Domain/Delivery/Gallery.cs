using System.Security.Cryptography;
using System.Text;
using Prata.Domain.Common;

namespace Prata.Domain.Delivery;

public sealed class Gallery : AggregateRoot, ITenantOwned
{
    private readonly List<Photo> _photos = [];
    private readonly List<Selection> _selections = [];

    private Gallery() { }

    private Gallery(
        Guid id,
        Guid tenantId,
        Guid orderId,
        int photoLimit,
        PortfolioConsent portfolioConsent,
        bool hasMinor,
        DateTimeOffset expiresAt,
        DateTimeOffset createdAt
    )
        : base(id)
    {
        TenantId = tenantId;
        OrderId = orderId;
        PhotoLimit = photoLimit;
        PortfolioConsent = portfolioConsent;
        HasMinor = hasMinor;
        ExpiresAt = expiresAt;
        CreatedAt = createdAt;
        Status = GalleryStatus.EmPreparo;
    }

    public Guid TenantId { get; }

    public Guid OrderId { get; }

    public GalleryStatus Status { get; private set; }

    public int PhotoLimit { get; }

    public DateTimeOffset? SelectionDeadline { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public PortfolioConsent PortfolioConsent { get; }

    public bool HasMinor { get; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset? ArchivedAt { get; private set; }

    public IReadOnlyList<Photo> Photos => _photos;

    public IReadOnlyList<Selection> Selections => _selections;

    public int ExtraPhotosCount => Math.Max(0, _selections.Count - PhotoLimit);

    public bool PermiteVisualizacaoBaixa =>
        Status
            is GalleryStatus.Disponivel
                or GalleryStatus.EmSelecao
                or GalleryStatus.SelecaoFechada
                or GalleryStatus.Entregue
                or GalleryStatus.BloqueadaPorPendencia;

    public bool PermiteAltaResolucao =>
        Status
            is GalleryStatus.Disponivel
                or GalleryStatus.EmSelecao
                or GalleryStatus.SelecaoFechada
                or GalleryStatus.Entregue;

    public bool PodePublicarNoPortfolio =>
        !HasMinor && PortfolioConsent is PortfolioConsent.Sim or PortfolioConsent.SomenteSemRosto;

    public static Result<Gallery> Create(
        Guid tenantId,
        Guid orderId,
        int photoLimit,
        PortfolioConsent portfolioConsent,
        bool hasMinor,
        DateTimeOffset expiresAt,
        DateTimeOffset agora
    )
    {
        if (photoLimit <= 0)
            return Error.Validation("GALERIA_LIMITE_INVALIDO", "Limite de fotos deve ser positivo.");

        return new Gallery(
            Guid.NewGuid(),
            tenantId,
            orderId,
            photoLimit,
            portfolioConsent,
            hasMinor,
            expiresAt,
            agora
        );
    }

    public Result<Unit> AdicionarFoto(Photo photo)
    {
        if (Status is not (GalleryStatus.EmPreparo or GalleryStatus.Disponivel))
            return Error.Validation("GALERIA_UPLOAD_BLOQUEADO", "Upload so em EmPreparo ou Disponivel.");

        if (_photos.Any(p => p.OriginalHash == photo.OriginalHash))
            return DeliveryErrors.FotoHashDuplicado;

        _photos.Add(photo);
        return Unit.Value;
    }

    public Result<Unit> Executar(string transicao, GalleryExecContext ctx) =>
        transicao switch
        {
            "Disponibilizar" => Disponibilizar(ctx.TodasDerivadasProntas),
            "BloquearPorPendencia" => BloquearPorPendencia(),
            "Desbloquear" => Desbloquear(),
            "IniciarSelecao" => IniciarSelecao(),
            "FecharSelecao" => FecharSelecao(ctx.UpsellConfirmado, ctx.PeloJob),
            "Entregar" => Entregar(ctx.SaldoConfirmado),
            "Expirar" => Expirar(),
            "Arquivar" => Arquivar(DateTimeOffset.UtcNow),
            "Reativar" => Reativar(),
            _ => DeliveryErrors.TransicaoInvalida,
        };

    public Result<Unit> Disponibilizar(bool todasDerivadasProntas)
    {
        if (Status != GalleryStatus.EmPreparo)
            return DeliveryErrors.TransicaoInvalida;
        if (!todasDerivadasProntas)
            return DeliveryErrors.DerivadasPendentes;

        Status = GalleryStatus.Disponivel;
        return Unit.Value;
    }

    public Result<Unit> BloquearPorPendencia()
    {
        if (Status != GalleryStatus.Disponivel)
            return DeliveryErrors.TransicaoInvalida;
        Status = GalleryStatus.BloqueadaPorPendencia;
        return Unit.Value;
    }

    public Result<Unit> Desbloquear()
    {
        if (Status != GalleryStatus.BloqueadaPorPendencia)
            return DeliveryErrors.TransicaoInvalida;
        Status = GalleryStatus.Disponivel;
        return Unit.Value;
    }

    public Result<Unit> IniciarSelecao()
    {
        if (Status != GalleryStatus.Disponivel)
            return DeliveryErrors.TransicaoInvalida;
        Status = GalleryStatus.EmSelecao;
        return Unit.Value;
    }

    public Result<Unit> Selecionar(Guid photoId, Guid selectedBy)
    {
        if (Status != GalleryStatus.EmSelecao)
            return Error.Validation("SELECAO_STATUS_INVALIDO", "Selecao so em EmSelecao.");

        if (_selections.Any(s => s.PhotoId == photoId))
            return Unit.Value;

        _selections.Add(Selection.Create(TenantId, Id, photoId, selectedBy, DateTimeOffset.UtcNow));
        return Unit.Value;
    }

    public Result<Unit> FecharSelecao(bool upsellConfirmado, bool peloJob)
    {
        if (Status != GalleryStatus.EmSelecao)
            return DeliveryErrors.TransicaoInvalida;

        if (!peloJob && _selections.Count > PhotoLimit && !upsellConfirmado)
            return DeliveryErrors.SelecaoExcede;

        Status = GalleryStatus.SelecaoFechada;
        return Unit.Value;
    }

    public Result<Unit> Entregar(bool saldoConfirmado)
    {
        if (Status != GalleryStatus.SelecaoFechada)
            return DeliveryErrors.TransicaoInvalida;
        if (!saldoConfirmado)
            return Error.Validation("GALERIA_SALDO_PENDENTE", "Entrega exige saldo Confirmado (RN-ENT-021).");

        Status = GalleryStatus.Entregue;
        return Unit.Value;
    }

    public bool PodeBaixarOriginal(bool saldoConfirmado) =>
        Status is GalleryStatus.SelecaoFechada or GalleryStatus.Entregue && saldoConfirmado;

    public Result<Unit> Expirar()
    {
        if (
            Status
            is not (
                GalleryStatus.Disponivel
                or GalleryStatus.EmSelecao
                or GalleryStatus.SelecaoFechada
                or GalleryStatus.Entregue
                or GalleryStatus.BloqueadaPorPendencia
            )
        )
            return DeliveryErrors.TransicaoInvalida;

        Status = GalleryStatus.Expirada;
        return Unit.Value;
    }

    public Result<Unit> Arquivar(DateTimeOffset agora)
    {
        if (Status != GalleryStatus.Expirada)
            return DeliveryErrors.TransicaoInvalida;
        Status = GalleryStatus.Arquivada;
        ArchivedAt = agora;
        return Unit.Value;
    }

    public Result<Unit> Reativar()
    {
        if (Status != GalleryStatus.Arquivada)
            return DeliveryErrors.TransicaoInvalida;
        Status = GalleryStatus.Disponivel;
        ArchivedAt = null;
        return Unit.Value;
    }
}

public sealed class Selection : Entity, ITenantOwned
{
    private Selection() { }

    private Selection(Guid id, Guid tenantId, Guid galleryId, Guid photoId, Guid selectedBy, DateTimeOffset selectedAt)
        : base(id)
    {
        TenantId = tenantId;
        GalleryId = galleryId;
        PhotoId = photoId;
        SelectedBy = selectedBy;
        SelectedAt = selectedAt;
    }

    public Guid TenantId { get; }

    public Guid GalleryId { get; }

    public Guid PhotoId { get; }

    public Guid SelectedBy { get; }

    public DateTimeOffset SelectedAt { get; }

    public static Selection Create(
        Guid tenantId,
        Guid galleryId,
        Guid photoId,
        Guid selectedBy,
        DateTimeOffset selectedAt
    ) =>
        new(Guid.NewGuid(), tenantId, galleryId, photoId, selectedBy, selectedAt);
}

public sealed class Photo : Entity, ITenantOwned
{
    private readonly List<PhotoVariant> _variants = [];

    private Photo()
    {
        OriginalKey = null!;
        OriginalHash = null!;
    }

    private Photo(
        Guid id,
        Guid tenantId,
        Guid galleryId,
        string originalKey,
        long originalBytes,
        string originalHash,
        int width,
        int height,
        int sortOrder
    )
        : base(id)
    {
        TenantId = tenantId;
        GalleryId = galleryId;
        OriginalKey = originalKey;
        OriginalBytes = originalBytes;
        OriginalHash = originalHash;
        Width = width;
        Height = height;
        SortOrder = sortOrder;
    }

    public Guid TenantId { get; }

    public Guid GalleryId { get; }

    public string OriginalKey { get; }

    public long OriginalBytes { get; }

    public string OriginalHash { get; }

    public DateTimeOffset? TakenAt { get; private set; }

    public int Width { get; }

    public int Height { get; }

    public int SortOrder { get; private set; }

    public bool IsFavoriteByStudio { get; private set; }

    public IReadOnlyList<PhotoVariant> Variants => _variants;

    public static Result<Photo> Create(
        Guid tenantId,
        Guid galleryId,
        string originalKey,
        long originalBytes,
        string originalHash,
        int width,
        int height,
        int sortOrder
    )
    {
        if (string.IsNullOrWhiteSpace(originalKey) || string.IsNullOrWhiteSpace(originalHash))
            return Error.Validation("FOTO_CHAVE_OBRIGATORIA", "Chave e hash do original sao obrigatorios.");

        return new Photo(
            Guid.NewGuid(),
            tenantId,
            galleryId,
            originalKey,
            originalBytes,
            originalHash,
            width,
            height,
            sortOrder
        );
    }

    public Result<Unit> RegistrarVariante(
        PhotoVariantKind kind,
        string storageKey,
        int width,
        int height,
        long bytes,
        bool hasWatermark,
        DateTimeOffset generatedAt
    )
    {
        if (kind == PhotoVariantKind.Original)
            return Error.Validation("VARIANTE_ORIGINAL_PROIBIDA", "Original nao e variante (RN-ENT-003).");

        if (_variants.Any(v => v.Kind == kind))
            return Error.Validation("VARIANTE_JA_EXISTE", "Variante deste kind ja existe.");

        _variants.Add(
            PhotoVariant.Create(TenantId, Id, kind, storageKey, width, height, bytes, hasWatermark, generatedAt)
        );
        return Unit.Value;
    }

    public bool TemThumbEWeb =>
        _variants.Any(v => v.Kind == PhotoVariantKind.Thumb) && _variants.Any(v => v.Kind == PhotoVariantKind.Web);
}

public sealed class PhotoVariant : Entity, ITenantOwned
{
    private PhotoVariant()
    {
        StorageKey = null!;
    }

    private PhotoVariant(
        Guid id,
        Guid tenantId,
        Guid photoId,
        PhotoVariantKind kind,
        string storageKey,
        int width,
        int height,
        long bytes,
        bool hasWatermark,
        DateTimeOffset generatedAt
    )
        : base(id)
    {
        TenantId = tenantId;
        PhotoId = photoId;
        Kind = kind;
        StorageKey = storageKey;
        Width = width;
        Height = height;
        Bytes = bytes;
        HasWatermark = hasWatermark;
        GeneratedAt = generatedAt;
    }

    public Guid TenantId { get; }

    public Guid PhotoId { get; }

    public PhotoVariantKind Kind { get; }

    public string StorageKey { get; }

    public int Width { get; }

    public int Height { get; }

    public long Bytes { get; }

    public bool HasWatermark { get; }

    public DateTimeOffset GeneratedAt { get; }

    public static PhotoVariant Create(
        Guid tenantId,
        Guid photoId,
        PhotoVariantKind kind,
        string storageKey,
        int width,
        int height,
        long bytes,
        bool hasWatermark,
        DateTimeOffset generatedAt
    ) =>
        new(Guid.NewGuid(), tenantId, photoId, kind, storageKey, width, height, bytes, hasWatermark, generatedAt);
}

public sealed class ShareLink : AggregateRoot, ITenantOwned
{
    private ShareLink()
    {
        TokenHash = null!;
        PasswordHash = null!;
    }

    private ShareLink(
        Guid id,
        Guid tenantId,
        Guid galleryId,
        string tokenHash,
        string passwordHash,
        DateTimeOffset expiresAt,
        bool allowFavorites,
        bool allowWebDownload,
        DateTimeOffset createdAt
    )
        : base(id)
    {
        TenantId = tenantId;
        GalleryId = galleryId;
        TokenHash = tokenHash;
        PasswordHash = passwordHash;
        ExpiresAt = expiresAt;
        AllowFavorites = allowFavorites;
        AllowWebDownload = allowWebDownload;
        CreatedAt = createdAt;
    }

    public Guid TenantId { get; }

    public Guid GalleryId { get; }

    public string TokenHash { get; }

    public string PasswordHash { get; }

    public DateTimeOffset ExpiresAt { get; }

    public bool AllowFavorites { get; }

    /// <summary>Padrao E4: convidado pode baixar web com marca (confirmado).</summary>
    public bool AllowWebDownload { get; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public DateTimeOffset? LastAccessAt { get; private set; }

    public int AccessCount { get; private set; }

    /// <summary>Convidado nunca baixa original (RN-ENT-040).</summary>
    public bool PodeBaixarOriginal { get; } = false;

    public static Result<ShareLink> Create(
        Guid tenantId,
        Guid galleryId,
        string password,
        string tokenPlain,
        DateTimeOffset expiresAt,
        bool allowFavorites,
        bool allowWebDownload,
        DateTimeOffset agora
    )
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
            return DeliveryErrors.ShareSenhaCurta;

        return new ShareLink(
            Guid.NewGuid(),
            tenantId,
            galleryId,
            Hash(tokenPlain),
            Hash(password),
            expiresAt,
            allowFavorites,
            allowWebDownload,
            agora
        );
    }

    public Result<Unit> Autenticar(string password, DateTimeOffset agora)
    {
        if (RevokedAt is not null || agora > ExpiresAt)
            return Error.Validation("SHARE_EXPIRADO", "Link expirado ou revogado.");
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(PasswordHash),
                Encoding.UTF8.GetBytes(Hash(password))
            ))
            return Error.Validation("SHARE_SENHA_INVALIDA", "Senha incorreta.");

        LastAccessAt = agora;
        AccessCount++;
        return Unit.Value;
    }

    public void Revogar(DateTimeOffset agora) => RevokedAt = agora;

    private static string Hash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes);
    }
}

public sealed class DownloadJob : AggregateRoot, ITenantOwned
{
    private DownloadJob()
    {
        Scope = null!;
    }

    private DownloadJob(
        Guid id,
        Guid tenantId,
        Guid galleryId,
        string scope,
        Guid requestedBy,
        DateTimeOffset expiresAt,
        DateTimeOffset createdAt
    )
        : base(id)
    {
        TenantId = tenantId;
        GalleryId = galleryId;
        Scope = scope;
        RequestedBy = requestedBy;
        ExpiresAt = expiresAt;
        CreatedAt = createdAt;
        Status = DownloadJobStatus.Pendente;
    }

    public Guid TenantId { get; }

    public Guid GalleryId { get; }

    public string Scope { get; }

    public DownloadJobStatus Status { get; private set; }

    public string? StorageKey { get; private set; }

    public long? Bytes { get; private set; }

    public DateTimeOffset ExpiresAt { get; }

    public Guid RequestedBy { get; }

    public DateTimeOffset CreatedAt { get; }

    public static DownloadJob Create(
        Guid tenantId,
        Guid galleryId,
        string scope,
        Guid requestedBy,
        DateTimeOffset expiresAt,
        DateTimeOffset agora
    ) =>
        new(Guid.NewGuid(), tenantId, galleryId, scope, requestedBy, expiresAt, agora);

    public void MarcarPronto(string storageKey, long bytes)
    {
        Status = DownloadJobStatus.Pronto;
        StorageKey = storageKey;
        Bytes = bytes;
    }
}
