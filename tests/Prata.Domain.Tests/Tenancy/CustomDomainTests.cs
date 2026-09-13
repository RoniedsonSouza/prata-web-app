using AwesomeAssertions;
using Prata.Domain.Tenancy;

namespace Prata.Domain.Tests.Tenancy;

public class CustomDomainTests
{
    [Fact]
    public void RN_TEN_013_solicita_dominio_proprio_e_marca_verificado()
    {
        var tenant = Tenant.Create("Studio", "studio-dom", DateTimeOffset.UtcNow).Value;
        tenant.SolicitarDominioProprio("fotos.studio.com.br").IsSuccess.Should().BeTrue();
        tenant.CustomDomain.Should().Be("fotos.studio.com.br");
        tenant.CustomDomainStatus.Should().Be(CustomDomainStatus.PendenteVerificacao);

        tenant.MarcarDominioVerificado(DateTimeOffset.UtcNow).IsSuccess.Should().BeTrue();
        tenant.CustomDomainStatus.Should().Be(CustomDomainStatus.Ativo);
    }

    [Fact]
    public void RN_TEN_013_rejeita_subdominio_da_plataforma()
    {
        var tenant = Tenant.Create("Studio", "studio-dom2", DateTimeOffset.UtcNow).Value;
        tenant.SolicitarDominioProprio("studio-dom2.prata.app").IsFailure.Should().BeTrue();
    }
}
