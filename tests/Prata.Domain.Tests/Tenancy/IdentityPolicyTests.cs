using AwesomeAssertions;
using Prata.Domain.Tenancy;

namespace Prata.Domain.Tests.Tenancy;

public class PasswordPolicyTests
{
    [Fact]
    public void RN_TEN_010_senha_com_menos_de_10_caracteres_falha()
    {
        var result = PasswordPolicy.Validate("curta");

        result.IsFailure.Should().BeTrue();
        result.Error!.Value.Code.Should().Be("SENHA_CURTA");
    }

    [Fact]
    public void RN_TEN_010_senha_com_10_caracteres_passa()
    {
        PasswordPolicy.Validate("senha12345").IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void RN_TEN_010_lockout_e_5_falhas_em_15_minutos()
    {
        PasswordPolicy.MaxFailedAttempts.Should().Be(5);
        PasswordPolicy.LockoutDuration.Should().Be(TimeSpan.FromMinutes(15));
    }
}

public class TeamPolicyTests
{
    [Fact]
    public void RN_TEN_007_nao_remove_ultimo_owner()
    {
        var result = TeamPolicy.PodeRemoverOuRebaixarOwner(targetIsOwner: true, activeOwnerCount: 1);

        result.IsFailure.Should().BeTrue();
        result.Error!.Value.Code.Should().Be("ULTIMO_OWNER");
    }

    [Fact]
    public void RN_TEN_007_permite_remover_owner_quando_ha_outro()
    {
        TeamPolicy
            .PodeRemoverOuRebaixarOwner(targetIsOwner: true, activeOwnerCount: 2)
            .IsSuccess.Should()
            .BeTrue();
    }

    [Fact]
    public void RN_TEN_007_permite_remover_staff()
    {
        TeamPolicy
            .PodeRemoverOuRebaixarOwner(targetIsOwner: false, activeOwnerCount: 1)
            .IsSuccess.Should()
            .BeTrue();
    }
}

public class RefreshTokenFamilyTests
{
    [Fact]
    public void Rotacao_valida_avanca_hash()
    {
        var agora = DateTimeOffset.UtcNow;
        var family = RefreshTokenFamily.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "hash1",
            agora,
            TimeSpan.FromDays(30)
        );

        family.Rotate("hash1", "hash2", agora).IsSuccess.Should().BeTrue();
        family.CurrentTokenHash.Should().Be("hash2");
    }

    [Fact]
    public void Reuso_de_token_antigo_revoga_familia()
    {
        var agora = DateTimeOffset.UtcNow;
        var family = RefreshTokenFamily.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "hash1",
            agora,
            TimeSpan.FromDays(30)
        );
        family.Rotate("hash1", "hash2", agora);

        var reuse = family.Rotate("hash1", "hash3", agora);

        reuse.IsFailure.Should().BeTrue();
        reuse.Error!.Value.Code.Should().Be("REFRESH_REUSADO");
        family.IsRevoked.Should().BeTrue();
    }
}
