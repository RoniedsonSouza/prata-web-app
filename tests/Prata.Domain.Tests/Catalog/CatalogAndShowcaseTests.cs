using AwesomeAssertions;
using Prata.Domain.Catalog;
using Prata.Domain.Common;
using Prata.Domain.Showcase;

namespace Prata.Domain.Tests.Catalog;

public class PackageTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _serviceTypeId = Guid.NewGuid();

    [Fact]
    public void RN_CAT_003_nao_publica_sem_campos_completos()
    {
        var package = Package.Create(_tenantId, _serviceTypeId, "Essencial").Value;

        var result = package.Publicar();

        result.IsFailure.Should().BeTrue();
        result.Error!.Value.Code.Should().Be("PACOTE_PUBLICACAO_INCOMPLETA");
    }

    [Fact]
    public void RN_CAT_003_publica_com_nome_preco_e_fotos()
    {
        var package = Package.Create(_tenantId, _serviceTypeId, "Essencial", Money.Brl(2500m), 100).Value;

        var result = package.Publicar();

        result.IsSuccess.Should().BeTrue();
        package.Status.Should().Be(PackageStatus.Publicado);
    }

    [Fact]
    public void RN_CAT_005_desativar_nao_altera_identidade_do_pacote()
    {
        // Pedido ainda nao existe na E1; o contrato e: desativar so muda Status.
        var package = Package.Create(_tenantId, _serviceTypeId, "Essencial", Money.Brl(2500m), 100).Value;
        package.Publicar();
        var id = package.Id;
        var price = package.Price;

        package.Desativar().IsSuccess.Should().BeTrue();

        package.Id.Should().Be(id);
        package.Price.Should().Be(price);
        package.Status.Should().Be(PackageStatus.Inativo);
    }

    [Fact]
    public void Atualizar_rejeita_pacote_inativo()
    {
        var package = Package.Create(_tenantId, _serviceTypeId, "Essencial", Money.Brl(2500m), 100).Value;
        package.Desativar();

        var result = package.Atualizar("Novo", Money.Brl(3000m), 120, "desc");

        result.IsFailure.Should().BeTrue();
        result.Error!.Value.Code.Should().Be("PACOTE_JA_INATIVO");
    }

    [Fact]
    public void Atualizar_altera_campos_editaveis()
    {
        var package = Package.Create(_tenantId, _serviceTypeId, "Essencial", Money.Brl(2500m), 100).Value;

        package.Atualizar("Premium", Money.Brl(3200m), 150, "Full day").IsSuccess.Should().BeTrue();

        package.Name.Should().Be("Premium");
        package.Price!.Value.Amount.Should().Be(3200m);
        package.IncludedPhotos.Should().Be(150);
        package.Description.Should().Be("Full day");
    }

    [Fact]
    public void Seed_cria_dez_tipos_de_servico()
    {
        var seed = ServiceType.CreateDefaultSeed(_tenantId);

        seed.Should().HaveCount(10);
        seed.Select(s => s.Code).Should().OnlyHaveUniqueItems();
    }
}

public class CollectionTests
{
    [Fact]
    public void RN_VIT_001_nao_publica_sem_item_e_capa()
    {
        var collection = Collection.Create(Guid.NewGuid(), "casamentos", "Casamentos").Value;

        collection.Publicar().Error!.Value.Code.Should().Be("COLECAO_SEM_ITENS");

        var item = CollectionItem.Create(collection.TenantId, collection.Id, "key", "Noiva no altar", 0, PortfolioConsent.Sim).Value;
        collection.AdicionarItem(item);

        collection.Publicar().Error!.Value.Code.Should().Be("COLECAO_SEM_CAPA");
    }

    [Fact]
    public void RN_VIT_001_publica_com_item_e_capa()
    {
        var collection = Collection.Create(Guid.NewGuid(), "casamentos", "Casamentos").Value;
        var item = CollectionItem.Create(collection.TenantId, collection.Id, "key", "Noiva no altar", 0, PortfolioConsent.Sim).Value;
        collection.AdicionarItem(item);
        collection.DefinirCapa(item.Id);

        var result = collection.Publicar();

        result.IsSuccess.Should().BeTrue();
        collection.Status.Should().Be(CollectionStatus.Publicada);
        collection.DomainEvents.Should().ContainSingle(e => e is ColecaoPublicada);
    }

    [Fact]
    public void RN_VIT_005_rejeita_item_sem_consentimento()
    {
        var result = CollectionItem.Create(Guid.NewGuid(), Guid.NewGuid(), "key", "alt", 0, PortfolioConsent.Nao);

        result.IsFailure.Should().BeTrue();
        result.Error!.Value.Code.Should().Be("ITEM_SEM_CONSENTIMENTO");
    }
}
