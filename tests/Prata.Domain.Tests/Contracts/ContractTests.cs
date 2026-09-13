using AwesomeAssertions;
using Prata.Domain.Common;
using Prata.Domain.Contracts;

namespace Prata.Domain.Tests.Contracts;

public class ContractTests
{
    private static readonly Guid TenantId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid OrderId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    private static readonly DateTimeOffset Agora = new(2026, 9, 12, 15, 0, 0, TimeSpan.Zero);

    private static readonly string[] TresClausulas =
    [
        ContractRequiredClauses.GaleriaExpiracao,
        ContractRequiredClauses.ConsentimentoImagem,
        ContractRequiredClauses.ConsentimentoMenor,
    ];

    [Fact]
    public void RN_CTR_001_enviar_sem_hash_falha()
    {
        var contract = Contract.Create(TenantId, OrderId, "v1", TresClausulas, Agora, validadeDias: 7).Value;

        contract.Enviar("contracts/x.pdf", pdfSha256: " ", Agora).IsFailure.Should().BeTrue();
        contract
            .Enviar("contracts/x.pdf", "a".PadRight(64, 'b'), Agora)
            .IsSuccess.Should()
            .BeTrue();
        contract.Status.Should().Be(ContractStatus.Enviado);
        contract.PdfSha256.Should().HaveLength(64);
    }

    [Fact]
    public void RN_CTR_002_template_exige_tres_clausulas()
    {
        var incompleto = Contract.Create(
            TenantId,
            OrderId,
            "v1",
            [ContractRequiredClauses.GaleriaExpiracao],
            Agora,
            7
        );

        incompleto.IsFailure.Should().BeTrue();
        incompleto.Error!.Value.Code.Should().Be("CONTRATO_CLAUSULAS_OBRIGATORIAS");

        Contract.Create(TenantId, OrderId, "v1", TresClausulas, Agora, 7).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void RN_CTR_010_assinatura_exige_hash_ip_ua_timestamp()
    {
        var contract = CriarEnviado();
        contract.RegistrarVisualizacao("1.1.1.1", "UA", Agora.AddMinutes(1)).IsSuccess.Should().BeTrue();

        contract
            .Assinar("Nome", "a@b.com", contract.PdfSha256!, ip: "", "UA", Agora.AddMinutes(2))
            .IsFailure.Should()
            .BeTrue();

        var ok = contract.Assinar(
            "Nome Cliente",
            "cliente@ex.com",
            contract.PdfSha256!,
            "203.0.113.10",
            "Mozilla/5.0",
            Agora.AddMinutes(2)
        );
        ok.IsSuccess.Should().BeTrue();
        contract.Status.Should().Be(ContractStatus.Assinado);
        contract.Signature.Should().NotBeNull();
        contract.Signature!.Ip.Should().Be("203.0.113.10");
    }

    [Fact]
    public void RN_CTR_011_assinado_imutavel_correcao_gera_novo()
    {
        var contract = CriarAssinado();

        contract.Enviar("outro.pdf", "c".PadRight(64, 'd'), Agora).IsFailure.Should().BeTrue();
        contract.Assinar("X", "x@y.com", contract.PdfSha256!, "1.1.1.1", "UA", Agora).IsFailure.Should().BeTrue();

        var correcao = contract.CriarCorrecao("v1-fix", TresClausulas, Agora.AddDays(1), validadeDias: 7);
        correcao.IsSuccess.Should().BeTrue();
        correcao.Value.ReplacesContractId.Should().Be(contract.Id);
        correcao.Value.Status.Should().Be(ContractStatus.Rascunho);
    }

    [Fact]
    public void RN_CTR_030_expirado_nao_assina()
    {
        var contract = CriarEnviado();
        contract.Expirar(Agora.AddDays(8)).IsSuccess.Should().BeTrue();
        contract.Status.Should().Be(ContractStatus.Expirado);

        var falha = contract.Assinar(
            "N",
            "n@e.com",
            contract.PdfSha256!,
            "1.1.1.1",
            "UA",
            Agora.AddDays(9)
        );
        falha.IsFailure.Should().BeTrue();
        falha.Error!.Value.Code.Should().Be("CONTRATO_EXPIRADO");
    }

    [Fact]
    public void Maquina_de_estados_contrato_percorre_caminho_feliz()
    {
        var c = Contract.Create(TenantId, OrderId, "v1", TresClausulas, Agora, 7).Value;
        c.Status.Should().Be(ContractStatus.Rascunho);
        c.Enviar("k.pdf", "e".PadRight(64, 'f'), Agora).IsSuccess.Should().BeTrue();
        c.RegistrarVisualizacao("10.0.0.1", "UA", Agora.AddHours(1)).IsSuccess.Should().BeTrue();
        c.Status.Should().Be(ContractStatus.Visualizado);
        c.Assinar("Cli", "c@e.com", c.PdfSha256!, "10.0.0.1", "UA", Agora.AddHours(2)).IsSuccess.Should().BeTrue();
    }

    private static Contract CriarEnviado()
    {
        var c = Contract.Create(TenantId, OrderId, "v1", TresClausulas, Agora, 7).Value;
        c.Enviar("contracts/a.pdf", "a".PadRight(64, '0'), Agora).IsSuccess.Should().BeTrue();
        return c;
    }

    private static Contract CriarAssinado()
    {
        var c = CriarEnviado();
        c.Assinar("Nome", "n@e.com", c.PdfSha256!, "1.1.1.1", "UA", Agora.AddMinutes(5)).IsSuccess.Should().BeTrue();
        return c;
    }
}
