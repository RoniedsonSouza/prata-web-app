using Prata.Domain.Common;

namespace Prata.Domain.Showcase;

public static class ShowcaseErrors
{
    public static readonly Error PublicacaoSemItens = new("COLECAO_SEM_ITENS", "Colecao publicada exige ao menos um item.");

    public static readonly Error PublicacaoSemCapa = new("COLECAO_SEM_CAPA", "Colecao publicada exige uma capa definida.");

    public static readonly Error AltTextObrigatorio = new("ITEM_ALT_TEXT_OBRIGATORIO", "Alt text da imagem e obrigatorio.");

    public static readonly Error SlugInvalido = new("COLECAO_SLUG_INVALIDO", "Slug da colecao e invalido.");

    public static readonly Error ConsentimentoNegado = new(
        "ITEM_SEM_CONSENTIMENTO",
        "Foto com PortfolioConsent = Nao nao entra em colecao publica."
    );

    public static readonly Error ColecaoJaPublicada = new("COLECAO_JA_PUBLICADA", "Colecao ja esta publicada.");
}

public enum CollectionStatus
{
    Rascunho = 0,
    Publicada = 1,
}

public enum PortfolioConsent
{
    Sim = 0,
    Nao = 1,
    Pendente = 2,
}

public sealed record ColecaoPublicada(Guid TenantId, Guid CollectionId, string Slug) : DomainEvent;

public sealed record ColecaoDespublicada(Guid TenantId, Guid CollectionId) : DomainEvent;

public sealed class CollectionItem : Entity, ITenantOwned
{
    private CollectionItem()
    {
        ObjectKey = null!;
        AltText = null!;
    }

    private CollectionItem(
        Guid id,
        Guid tenantId,
        Guid collectionId,
        string objectKey,
        string altText,
        int sortOrder,
        bool isCover,
        PortfolioConsent consent
    )
        : base(id)
    {
        TenantId = tenantId;
        CollectionId = collectionId;
        ObjectKey = objectKey;
        AltText = altText;
        SortOrder = sortOrder;
        IsCover = isCover;
        Consent = consent;
    }

    public Guid TenantId { get; }

    public Guid CollectionId { get; }

    public string ObjectKey { get; }

    public string AltText { get; }

    public int SortOrder { get; private set; }

    public bool IsCover { get; private set; }

    public PortfolioConsent Consent { get; }

    public static Result<CollectionItem> Create(
        Guid tenantId,
        Guid collectionId,
        string objectKey,
        string altText,
        int sortOrder,
        PortfolioConsent consent,
        bool isCover = false
    )
    {
        if (string.IsNullOrWhiteSpace(altText))
            return ShowcaseErrors.AltTextObrigatorio;

        if (consent == PortfolioConsent.Nao)
            return ShowcaseErrors.ConsentimentoNegado;

        return new CollectionItem(Guid.NewGuid(), tenantId, collectionId, objectKey, altText.Trim(), sortOrder, isCover, consent);
    }

    internal void SetCover(bool isCover) => IsCover = isCover;

    internal void SetSortOrder(int sortOrder) => SortOrder = sortOrder;
}

/// <summary>
/// Colecao do portfolio publico. RN-VIT-001.
/// </summary>
public sealed class Collection : AggregateRoot, ITenantOwned
{
    private readonly List<CollectionItem> _items = [];

    private Collection()
    {
        Slug = null!;
        Title = null!;
    }

    private Collection(Guid id, Guid tenantId, string slug, string title, CollectionStatus status)
        : base(id)
    {
        TenantId = tenantId;
        Slug = slug;
        Title = title;
        Status = status;
    }

    public Guid TenantId { get; }

    public string Slug { get; }

    public string Title { get; private set; }

    public CollectionStatus Status { get; private set; }

    public IReadOnlyList<CollectionItem> Items => _items;

    public static Result<Collection> Create(Guid tenantId, string slug, string title)
    {
        if (string.IsNullOrWhiteSpace(slug) || slug.Length < 2)
            return ShowcaseErrors.SlugInvalido;

        if (string.IsNullOrWhiteSpace(title))
            return Error.Validation("COLECAO_TITULO_OBRIGATORIO", "Titulo da colecao e obrigatorio.");

        return new Collection(Guid.NewGuid(), tenantId, slug.Trim().ToLowerInvariant(), title.Trim(), CollectionStatus.Rascunho);
    }

    public Result<Unit> AdicionarItem(CollectionItem item)
    {
        if (item.CollectionId != Id || item.TenantId != TenantId)
            return Error.Validation("ITEM_COLECAO_INVALIDA", "Item nao pertence a esta colecao.");

        _items.Add(item);
        return Unit.Value;
    }

    public Result<Unit> DefinirCapa(Guid itemId)
    {
        var item = _items.FirstOrDefault(i => i.Id == itemId);
        if (item is null)
            return Error.Validation("ITEM_NAO_ENCONTRADO", "Item nao encontrado na colecao.");

        foreach (var existing in _items)
            existing.SetCover(existing.Id == itemId);

        return Unit.Value;
    }

    public Result<Unit> Publicar()
    {
        if (Status == CollectionStatus.Publicada)
            return ShowcaseErrors.ColecaoJaPublicada;

        if (_items.Count < 1)
            return ShowcaseErrors.PublicacaoSemItens;

        if (!_items.Any(i => i.IsCover))
            return ShowcaseErrors.PublicacaoSemCapa;

        Status = CollectionStatus.Publicada;
        Raise(new ColecaoPublicada(TenantId, Id, Slug));
        return Unit.Value;
    }

    public Result<Unit> Despublicar()
    {
        Status = CollectionStatus.Rascunho;
        Raise(new ColecaoDespublicada(TenantId, Id));
        return Unit.Value;
    }
}

public sealed class SeoMeta : ValueObject
{
    public SeoMeta(string title, string description, string? ogImageUrl = null)
    {
        Title = title;
        Description = description;
        OgImageUrl = ogImageUrl;
    }

    public string Title { get; }

    public string Description { get; }

    public string? OgImageUrl { get; }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Title;
        yield return Description;
        yield return OgImageUrl;
    }
}

public sealed class PageContent : AggregateRoot, ITenantOwned
{
    private PageContent()
    {
        Path = null!;
        BodyMarkdown = null!;
        Seo = null!;
    }

    private PageContent(Guid id, Guid tenantId, string path, string bodyMarkdown, SeoMeta seo)
        : base(id)
    {
        TenantId = tenantId;
        Path = path;
        BodyMarkdown = bodyMarkdown;
        Seo = seo;
    }

    public Guid TenantId { get; }

    public string Path { get; }

    public string BodyMarkdown { get; private set; }

    public SeoMeta Seo { get; private set; }

    public static PageContent Create(Guid tenantId, string path, string bodyMarkdown, SeoMeta seo) =>
        new(Guid.NewGuid(), tenantId, path, bodyMarkdown, seo);
}
