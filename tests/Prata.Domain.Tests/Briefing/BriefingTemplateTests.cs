using AwesomeAssertions;
using Prata.Application.Briefing;
using Prata.Domain.Briefing;

namespace Prata.Domain.Tests.Briefing;

public class BriefingTemplateTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid ServiceTypeId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public void RN_BRF_003_rejeita_visible_when_circular_ou_futuro()
    {
        var template = BriefingTemplate.Create(TenantId, ServiceTypeId, "Casamento").Value;
        var a1 = Question.Create(TenantId, template.Id, "A1", "Data?", QuestionType.Date, BriefingBlock.A, 1, true, false).Value;
        var a2 = Question
            .Create(
                TenantId,
                template.Id,
                "A2",
                "Local?",
                QuestionType.Text,
                BriefingBlock.A,
                2,
                true,
                false,
                visibleWhen: "A3.value >= 1"
            )
            .Value;
        var a3 = Question.Create(TenantId, template.Id, "A3", "Pessoas?", QuestionType.Number, BriefingBlock.A, 3, true, false).Value;

        template.AdicionarPergunta(a1).IsSuccess.Should().BeTrue();
        template.AdicionarPergunta(a2).IsSuccess.Should().BeTrue();
        template.AdicionarPergunta(a3).IsSuccess.Should().BeTrue();

        var pub = template.Publicar();

        pub.IsFailure.Should().BeTrue();
        pub.Error!.Value.Code.Should().Be("BRIEFING_VISIBLE_WHEN_CIRCULAR");
    }

    [Fact]
    public void RN_BRF_021_rejeita_pergunta_sensivel_em_formato_diagnostico()
    {
        var template = BriefingTemplate.Create(TenantId, ServiceTypeId, "Studio").Value;
        var bad = Question
            .Create(
                TenantId,
                template.Id,
                "B9",
                "Qual a causa da sua doenca?",
                QuestionType.Text,
                BriefingBlock.B,
                1,
                false,
                isSensitive: true
            )
            .Value;
        template.AdicionarPergunta(bad);

        var pub = template.Publicar();

        pub.IsFailure.Should().BeTrue();
        pub.Error!.Value.Code.Should().Be("BRIEFING_DIAGNOSTICO_PROIBIDO");
    }

    [Fact]
    public void RN_BRF_004_rejeita_valor_com_tipo_divergente()
    {
        var shape = AnswerValueValidator.Validate(QuestionType.Scale, """{"text":"nao e escala"}""");

        shape.IsFailure.Should().BeTrue();
        shape.Error!.Value.Code.Should().Be("BRIEFING_RESPOSTA_TIPO_DIVERGENTE");
    }

    [Fact]
    public void RN_BRF_020_bloco_b_sensivel_exige_consentimento()
    {
        var q = Question
            .Create(
                TenantId,
                Guid.NewGuid(),
                "B3",
                "Evitar nas imagens?",
                QuestionType.Chips,
                BriefingBlock.B,
                1,
                false,
                isSensitive: true
            )
            .Value;

        var answer = Answer.Create(
            TenantId,
            Guid.NewGuid(),
            q,
            """{"chips":["sorriso"],"text":""}""",
            valueShapeValid: true,
            hasSensitiveConsent: false
        );

        answer.IsFailure.Should().BeTrue();
        answer.Error!.Value.Code.Should().Be("BRIEFING_CONSENTIMENTO_AUSENTE");
    }

    [Fact]
    public void RN_BRF_002_answer_guarda_snapshot_do_prompt()
    {
        var q = Question
            .Create(TenantId, Guid.NewGuid(), "A2", "Cidade e local", QuestionType.Text, BriefingBlock.A, 1, true, false)
            .Value;

        var answer = Answer
            .Create(
                TenantId,
                Guid.NewGuid(),
                q,
                """{"text":"Sao Paulo"}""",
                valueShapeValid: true,
                hasSensitiveConsent: false
            )
            .Value;

        answer.PromptSnapshot.Should().Be("Cidade e local");
        answer.TypeSnapshot.Should().Be(QuestionType.Text);
        answer.QuestionCode.Should().Be("A2");
    }
}
