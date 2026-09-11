using Prata.Domain.Common;

namespace Prata.Domain.Catalog;

public enum PackageStatus
{
    Rascunho = 0,
    Publicado = 1,
    Inativo = 2,
}

public sealed record PacotePublicado(Guid TenantId, Guid PackageId) : DomainEvent;

public sealed record PacoteDespublicado(Guid TenantId, Guid PackageId) : DomainEvent;

public sealed record PrecoAlterado(Guid TenantId, Guid PackageId, decimal Amount, Currency Currency) : DomainEvent;

/// <summary>
/// Pacote comercial. Desativar nao afeta pedido existente (RN-CAT-005) —
/// pedidos carregam snapshot; este agregado so muda o catalogo.
/// </summary>
public sealed class Package : AggregateRoot, ITenantOwned
{
    private Package()
    {
        Name = null!;
    }

    private Package(
        Guid id,
        Guid tenantId,
        Guid serviceTypeId,
        string name,
        Money? price,
        int? includedPhotos,
        string? description,
        PackageStatus status
    )
        : base(id)
    {
        TenantId = tenantId;
        ServiceTypeId = serviceTypeId;
        Name = name;
        Price = price;
        IncludedPhotos = includedPhotos;
        Description = description;
        Status = status;
    }

    public Guid TenantId { get; }

    public Guid ServiceTypeId { get; }

    public string Name { get; private set; }

    public Money? Price { get; private set; }

    public int? IncludedPhotos { get; private set; }

    public string? Description { get; private set; }

    public PackageStatus Status { get; private set; }

    public static Result<Package> Create(
        Guid tenantId,
        Guid serviceTypeId,
        string name,
        Money? price = null,
        int? includedPhotos = null,
        string? description = null
    )
    {
        if (string.IsNullOrWhiteSpace(name))
            return CatalogErrors.NomeObrigatorio;

        if (price is { Amount: <= 0 })
            return CatalogErrors.PrecoInvalido;

        if (includedPhotos is <= 0)
            return CatalogErrors.FotosIncluidasInvalidas;

        return new Package(
            Guid.NewGuid(),
            tenantId,
            serviceTypeId,
            name.Trim(),
            price,
            includedPhotos,
            description?.Trim(),
            PackageStatus.Rascunho
        );
    }

    public Result<Unit> AtualizarPreco(Money price)
    {
        if (price.Amount <= 0)
            return CatalogErrors.PrecoInvalido;

        Price = price;
        Raise(new PrecoAlterado(TenantId, Id, price.Amount, price.Currency));
        return Unit.Value;
    }

    /// <summary>
    /// Atualiza campos editaveis do rascunho/publicado. Nao reativa pacote Inativo.
    /// </summary>
    public Result<Unit> Atualizar(
        string name,
        Money? price,
        int? includedPhotos,
        string? description
    )
    {
        if (Status == PackageStatus.Inativo)
            return CatalogErrors.PacoteJaInativo;

        if (string.IsNullOrWhiteSpace(name))
            return CatalogErrors.NomeObrigatorio;

        if (price is { Amount: <= 0 })
            return CatalogErrors.PrecoInvalido;

        if (includedPhotos is <= 0)
            return CatalogErrors.FotosIncluidasInvalidas;

        Name = name.Trim();
        Price = price;
        IncludedPhotos = includedPhotos;
        Description = description?.Trim();
        if (price is not null)
        {
            Raise(new PrecoAlterado(TenantId, Id, price.Value.Amount, price.Value.Currency));
        }

        return Unit.Value;
    }

    public Result<Unit> Publicar()
    {
        if (Status == PackageStatus.Publicado)
            return CatalogErrors.PacoteJaPublicado;

        if (string.IsNullOrWhiteSpace(Name) || Price is null || IncludedPhotos is null or <= 0)
            return CatalogErrors.PublicacaoIncompleta;

        Status = PackageStatus.Publicado;
        Raise(new PacotePublicado(TenantId, Id));
        return Unit.Value;
    }

    public Result<Unit> Despublicar()
    {
        if (Status != PackageStatus.Publicado)
            return CatalogErrors.PacoteNaoPublicado;

        Status = PackageStatus.Rascunho;
        Raise(new PacoteDespublicado(TenantId, Id));
        return Unit.Value;
    }

    /// <summary>
    /// Desativa o pacote no catalogo. Pedidos existentes nao sao tocados
    /// (RN-CAT-005) — eles guardam snapshot de nome/preco.
    /// </summary>
    public Result<Unit> Desativar()
    {
        if (Status == PackageStatus.Inativo)
            return CatalogErrors.PacoteJaInativo;

        Status = PackageStatus.Inativo;
        return Unit.Value;
    }
}
