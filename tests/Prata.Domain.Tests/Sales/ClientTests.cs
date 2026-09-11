using AwesomeAssertions;
using Prata.Domain.Sales;

namespace Prata.Domain.Tests.Sales;

public class ClientTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void RN_COM_012_cria_cliente_com_email_normalizado()
    {
        var result = Client.Create(
            TenantId,
            "Maria Silva",
            "  Maria@Exemplo.com ",
            whatsapp: "+5511999998888",
            PreferredChannel.WhatsApp
        );

        result.IsSuccess.Should().BeTrue();
        var client = result.Value;
        client.TenantId.Should().Be(TenantId);
        client.Name.Should().Be("Maria Silva");
        client.Email.Should().Be("maria@exemplo.com");
        client.WhatsApp.Should().Be("+5511999998888");
        client.PreferredChannel.Should().Be(PreferredChannel.WhatsApp);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("sem-arroba")]
    public void RN_COM_012_rejeita_email_invalido(string email)
    {
        var result = Client.Create(TenantId, "Maria", email, null, PreferredChannel.Email);

        result.IsFailure.Should().BeTrue();
        result.Error!.Value.Code.Should().Be("CLIENTE_EMAIL_INVALIDO");
    }

    [Fact]
    public void RN_COM_012_rejeita_nome_vazio()
    {
        var result = Client.Create(TenantId, "  ", "a@b.com", null, PreferredChannel.Email);

        result.IsFailure.Should().BeTrue();
        result.Error!.Value.Code.Should().Be("CLIENTE_NOME_OBRIGATORIO");
    }
}
