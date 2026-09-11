using Prata.Domain.Common;

namespace Prata.Domain.Tenancy;

/// <summary>
/// Agregado raiz do estudio. RN-TEN-001..012 (subset E1).
/// </summary>
public sealed class Tenant : AggregateRoot
{
    // EF Core
    private Tenant()
    {
        Slug = null!;
        Name = null!;
        Settings = null!;
    }

    private Tenant(Guid id, TenantSlug slug, string name, TenantSettings settings, TenantStatus status, DateTimeOffset createdAt)
        : base(id)
    {
        Slug = slug;
        Name = name;
        Settings = settings;
        Status = status;
        CreatedAt = createdAt;
        PortfolioPublishedAt = null;
    }

    public TenantSlug Slug { get; private set; }

    public string Name { get; private set; }

    public TenantSettings Settings { get; private set; }

    public TenantStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? PortfolioPublishedAt { get; private set; }

    public bool IsPortfolioPublished => PortfolioPublishedAt is not null;

    public static Result<Tenant> Create(string name, string slug, DateTimeOffset agora)
    {
        if (string.IsNullOrWhiteSpace(name))
            return TenancyErrors.NomeObrigatorio;

        var slugResult = TenantSlug.Create(slug);
        if (slugResult.IsFailure)
            return Result.Failure<Tenant>(slugResult.Error!.Value);

        var tenant = new Tenant(
            Guid.NewGuid(),
            slugResult.Value,
            name.Trim(),
            TenantSettings.CreateDefault(),
            TenantStatus.Rascunho,
            agora
        );

        tenant.Raise(new TenantCriado(tenant.Id, tenant.Slug.Value, tenant.Name));
        return tenant;
    }

    /// <summary>
    /// Altera o slug. Bloqueado apos a primeira publicacao do portfolio (RN-TEN-003).
    /// </summary>
    public Result<Unit> AlterarSlug(string novoSlug)
    {
        if (IsPortfolioPublished)
            return TenancyErrors.SlugImutavelAposPublicacao;

        var slugResult = TenantSlug.Create(novoSlug);
        if (slugResult.IsFailure)
            return Result.Failure<Unit>(slugResult.Error!.Value);

        Slug = slugResult.Value;
        return Unit.Value;
    }

    public Result<Unit> PublicarPortfolio(DateTimeOffset agora)
    {
        if (Status == TenantStatus.Suspenso)
            return TenancyErrors.TenantSuspenso;

        if (IsPortfolioPublished)
            return TenancyErrors.TenantJaPublicado;

        PortfolioPublishedAt = agora;
        Status = TenantStatus.Ativo;
        Raise(new PortfolioPublicado(Id, Slug.Value));
        return Unit.Value;
    }

    public Result<Unit> Suspender(string reason)
    {
        if (Status == TenantStatus.Suspenso)
            return TenancyErrors.TenantJaSuspenso;

        Status = TenantStatus.Suspenso;
        Raise(new TenantSuspenso(Id, reason));
        return Unit.Value;
    }

    public Result<Unit> Reativar()
    {
        if (Status != TenantStatus.Suspenso)
            return TenancyErrors.TenantNaoSuspenso;

        Status = IsPortfolioPublished ? TenantStatus.Ativo : TenantStatus.Rascunho;
        Raise(new TenantReativado(Id));
        return Unit.Value;
    }

    public Result<Unit> AlterarTema(ThemeId themeId)
    {
        // E1: so editorial. Cinema/imersivo entram na E4.
        if (themeId is not ThemeId.Editorial)
            return Error.Validation("TEMA_INDISPONIVEL", "Apenas o tema editorial esta disponivel nesta etapa.");

        Settings = Settings.WithTheme(themeId);
        Raise(new TemaAlterado(Id, themeId));
        return Unit.Value;
    }
}
