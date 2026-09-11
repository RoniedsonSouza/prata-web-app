using AwesomeAssertions;
using Prata.Domain.Tenancy;

namespace Prata.Domain.Tests.Tenancy;

public class TenantSlugTests
{
    [Theory]
    [InlineData("joao-silva")]
    [InlineData("abc")]
    [InlineData("a1b")]
    [InlineData("estudio-foto-2026")]
    public void RN_TEN_003_aceita_slug_valido(string slug)
    {
        var result = TenantSlug.Create(slug);

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be(slug);
    }

    [Theory]
    [InlineData("")]
    [InlineData("ab")]
    [InlineData("-abc")]
    [InlineData("abc-")]
    [InlineData("Joao")]
    [InlineData("joao_silva")]
    [InlineData("joão")]
    public void RN_TEN_003_rejeita_slug_invalido(string slug)
    {
        var result = TenantSlug.Create(slug);

        result.IsFailure.Should().BeTrue();
        result.Error!.Value.Code.Should().Be("TENANT_SLUG_INVALIDO");
    }

    [Theory]
    [InlineData("www")]
    [InlineData("api")]
    [InlineData("prata")]
    [InlineData("admin")]
    [InlineData("console")]
    public void RN_TEN_003_rejeita_slug_reservado(string slug)
    {
        var result = TenantSlug.Create(slug);

        result.IsFailure.Should().BeTrue();
        result.Error!.Value.Code.Should().Be("TENANT_SLUG_RESERVADO");
    }
}

public class TenantTests
{
    private static readonly DateTimeOffset Agora = new(2026, 9, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Cria_tenant_em_rascunho_com_settings_padrao()
    {
        var result = Tenant.Create("Estudio Luz", "estudio-luz", Agora);

        result.IsSuccess.Should().BeTrue();
        var tenant = result.Value;
        tenant.Status.Should().Be(TenantStatus.Rascunho);
        tenant.Settings.ThemeId.Should().Be(ThemeId.Editorial);
        tenant.DomainEvents.Should().ContainSingle(e => e is TenantCriado);
    }

    [Fact]
    public void RN_TEN_003_slug_imutavel_apos_publicar_portfolio()
    {
        var tenant = Tenant.Create("Estudio Luz", "estudio-luz", Agora).Value;
        tenant.PublicarPortfolio(Agora).IsSuccess.Should().BeTrue();

        var change = tenant.AlterarSlug("outro-slug");

        change.IsFailure.Should().BeTrue();
        change.Error!.Value.Code.Should().Be("TENANT_SLUG_IMUTAVEL");
        tenant.Slug.Value.Should().Be("estudio-luz");
    }

    [Fact]
    public void RN_TEN_009_suspende_e_reativa()
    {
        var tenant = Tenant.Create("Estudio Luz", "estudio-luz", Agora).Value;
        tenant.PublicarPortfolio(Agora);

        tenant.Suspender("inadimplencia").IsSuccess.Should().BeTrue();
        tenant.Status.Should().Be(TenantStatus.Suspenso);

        tenant.Reativar().IsSuccess.Should().BeTrue();
        tenant.Status.Should().Be(TenantStatus.Ativo);
    }
}

public class InviteTests
{
    [Fact]
    public void RN_TEN_008_convite_expira_em_7_dias()
    {
        var agora = new DateTimeOffset(2026, 9, 10, 0, 0, 0, TimeSpan.Zero);
        var invite = Invite.Create(Guid.NewGuid(), "ana@email.com", "tenant.staff", "hash", agora).Value;

        invite.ExpiresAt.Should().Be(agora.AddDays(7));

        var aceitar = invite.Aceitar(Guid.NewGuid(), agora.AddDays(8));

        aceitar.IsFailure.Should().BeTrue();
        aceitar.Error!.Value.Code.Should().Be("CONVITE_EXPIRADO");
        invite.Status.Should().Be(InviteStatus.Expirado);
    }
}
