using Prata.Domain.Briefing;
using Prata.Domain.Catalog;

namespace Prata.Infrastructure.Briefing;

/// <summary>
/// Copia templates semente por ServiceType (RN-BRF-001). Composicao alinhada a docs/07 §4.
/// </summary>
public static class BriefingTemplateSeeder
{
    public const string DefaultPurposeText =
        "Estas perguntas sao opcionais e servem so para o fotografo conduzir melhor o seu ensaio. "
        + "So quem vai fotografar voce tem acesso. Voce pode deixar em branco ou apagar depois, sem prejuizo ao pedido.";

    public static IReadOnlyList<BriefingTemplate> CreateForTenant(
        Guid tenantId,
        IReadOnlyList<ServiceType> serviceTypes
    )
    {
        var templates = new List<BriefingTemplate>();
        foreach (var st in serviceTypes)
        {
            var template = BriefingTemplate.Create(tenantId, st.Id, $"Briefing · {st.Name}").Value;
            foreach (var def in QuestionsFor(st.Code))
            {
                var q = Question
                    .Create(
                        tenantId,
                        template.Id,
                        def.Code,
                        def.Prompt,
                        def.Type,
                        def.Block,
                        def.SortOrder,
                        def.IsRequired,
                        def.IsSensitive,
                        def.VisibleWhen
                    )
                    .Value;
                foreach (var opt in def.Options)
                    q.AdicionarOpcao(opt.Code, opt.Label, opt.SortOrder, opt.Chip);
                template.AdicionarPergunta(q);
            }

            var published = template.Publicar();
            if (published.IsFailure)
            {
                throw new InvalidOperationException(
                    $"Falha ao publicar template semente {st.Code}: {published.Error!.Value.Code}"
                );
            }

            templates.Add(template);
        }

        return templates;
    }

    private static IEnumerable<QuestionDef> QuestionsFor(string serviceCode) =>
        serviceCode switch
        {
            "casamento-civil" or "formatura" => FullEvent(),
            "pre-wedding" => PreWedding(),
            "gestante" => Gestante(),
            "newborn" => Newborn(),
            "aniversario-infantil" or "ensaio-familia" => FamilyKids(),
            "book-15-anos" or "studio" => StudioBook(),
            "corporativo" => Corporativo(),
            _ => StudioBook(),
        };

    private static IEnumerable<QuestionDef> FullEvent() =>
        BaseLogistics(includePeopleCount: true, includeSchedule: true, includeExtras: true)
            .Concat(BlockBFull())
            .Concat(BlockCFull())
            .Concat(BlockD())
            .Concat(BlockE());

    private static IEnumerable<QuestionDef> PreWedding() =>
        BaseLogistics(includePeopleCount: false, includeSchedule: false, includeExtras: true)
            .Concat(BlockBFull())
            .Concat([Q("C5", "Ha algo da historia do casal que queira registrar?", QuestionType.Text, BriefingBlock.C, false, false)])
            .Concat(BlockD())
            .Concat(BlockE());

    private static IEnumerable<QuestionDef> Gestante() =>
        BaseLogistics(includePeopleCount: false, includeSchedule: false, includeExtras: false)
            .Append(Q("A7", "Precisa de pausas durante o ensaio?", QuestionType.SingleChoice, BriefingBlock.A, false, false, opts: YesNo()))
            .Append(Q("A8", "Alguma restricao de deslocamento no local?", QuestionType.Chips, BriefingBlock.A, false, false))
            .Concat(BlockBFull())
            .Concat(BlockD())
            .Concat(BlockE());

    private static IEnumerable<QuestionDef> Newborn() =>
        new QuestionDef[]
        {
            Q("A1", "Data e horario previstos", QuestionType.Date, BriefingBlock.A, true, false),
            Q("A2", "Cidade e local", QuestionType.Text, BriefingBlock.A, true, false),
            Q("A4", "Ambiente: interno · externo · os dois · ainda nao definido", QuestionType.SingleChoice, BriefingBlock.A, true, false, opts: Ambiente()),
            Q("A7", "Precisa de pausas durante o ensaio?", QuestionType.SingleChoice, BriefingBlock.A, false, false, opts: YesNo()),
            Q("B1", "Voce gosta de sorrir nas fotos? 1 serio → 5 sorriso aberto", QuestionType.Scale, BriefingBlock.B, false, false),
            Q("B4", "Nivel de conforto diante da camera. 1 travo → 5 adoro posar", QuestionType.Scale, BriefingBlock.B, false, false),
            Q("B8", "Qual foto sua voce mais gosta e por que?", QuestionType.Upload, BriefingBlock.B, false, false),
            Q("B10", "Como prefere ser chamado(a) durante o ensaio?", QuestionType.Text, BriefingBlock.B, false, false),
            Q("C5", "Ha algo da historia da familia que queira registrar?", QuestionType.Text, BriefingBlock.C, false, false),
        }
            .Concat(BlockD())
            .Concat(BlockE());

    private static IEnumerable<QuestionDef> FamilyKids() =>
        BaseLogistics(includePeopleCount: true, includeSchedule: false, includeExtras: false)
            .Concat(
                new QuestionDef[]
                {
                    Q("B1", "Voce gosta de sorrir nas fotos? 1 serio → 5 sorriso aberto", QuestionType.Scale, BriefingBlock.B, false, false),
                    Q("B4", "Nivel de conforto diante da camera. 1 travo → 5 adoro posar", QuestionType.Scale, BriefingBlock.B, false, false),
                    Q("B10", "Como prefere ser chamado(a) durante o ensaio?", QuestionType.Text, BriefingBlock.B, false, false),
                }
            )
            .Concat(BlockCFull())
            .Concat(BlockD())
            .Concat(BlockE());

    private static IEnumerable<QuestionDef> StudioBook() =>
        new QuestionDef[]
        {
            Q("A1", "Data e horario previstos", QuestionType.Date, BriefingBlock.A, true, false),
            Q("A2", "Cidade e local", QuestionType.Text, BriefingBlock.A, true, false),
            Q("A4", "Ambiente: interno · externo · os dois · ainda nao definido", QuestionType.SingleChoice, BriefingBlock.A, true, false, opts: Ambiente()),
        }
            .Concat(BlockBFull())
            .Concat(BlockD())
            .Concat(BlockE());

    private static IEnumerable<QuestionDef> Corporativo() =>
        BaseLogistics(includePeopleCount: true, includeSchedule: false, includeExtras: false)
            .Concat(
                new QuestionDef[]
                {
                    Q("B1", "Voce gosta de sorrir nas fotos? 1 serio → 5 sorriso aberto", QuestionType.Scale, BriefingBlock.B, false, false),
                    Q("B2", "Lado preferido do rosto", QuestionType.SingleChoice, BriefingBlock.B, false, false, opts: LadoRosto()),
                    Q("B4", "Nivel de conforto diante da camera. 1 travo → 5 adoro posar", QuestionType.Scale, BriefingBlock.B, false, false),
                    Q("B6", "Enquadramento preferido", QuestionType.SingleChoice, BriefingBlock.B, false, false, opts: Enquadramento()),
                    Q("B7", "Usa oculos? Prefere manter nas fotos?", QuestionType.SingleChoice, BriefingBlock.B, false, false, opts: Oculos()),
                    Q("D1", "Referencias visuais (links)", QuestionType.Url, BriefingBlock.D, false, false),
                    Q("D3", "Cores ou estilos a evitar", QuestionType.Text, BriefingBlock.D, false, false),
                    Q("D4", "Autoriza uso de imagem em portfolio?", QuestionType.SingleChoice, BriefingBlock.D, true, false, opts: YesNo()),
                    Q("E1", "Algo mais que o fotografo precise saber?", QuestionType.Text, BriefingBlock.E, false, false),
                    Q("E4", "Canal preferido para alinhamentos", QuestionType.SingleChoice, BriefingBlock.E, false, false, opts: Canal()),
                }
            );

    private static IEnumerable<QuestionDef> BaseLogistics(bool includePeopleCount, bool includeSchedule, bool includeExtras)
    {
        yield return Q("A1", "Data e horario previstos", QuestionType.Date, BriefingBlock.A, true, false);
        yield return Q("A2", "Cidade e local", QuestionType.Text, BriefingBlock.A, true, false);
        if (includePeopleCount)
            yield return Q("A3", "Numero aproximado de pessoas", QuestionType.Number, BriefingBlock.A, false, false);
        yield return Q("A4", "Ambiente: interno · externo · os dois · ainda nao definido", QuestionType.SingleChoice, BriefingBlock.A, true, false, opts: Ambiente());
        if (includeSchedule)
            yield return Q("A5", "Ja existe cronograma do dia?", QuestionType.SingleChoice, BriefingBlock.A, false, false, opts: YesNo());
        if (includeExtras)
            yield return Q("A6", "Extras desejados", QuestionType.MultiChoice, BriefingBlock.A, false, false, opts: Extras());
    }

    private static IEnumerable<QuestionDef> BlockBFull() =>
        [
            Q("B1", "Voce gosta de sorrir nas fotos? 1 serio → 5 sorriso aberto", QuestionType.Scale, BriefingBlock.B, false, false),
            Q("B2", "Lado preferido do rosto", QuestionType.SingleChoice, BriefingBlock.B, false, false, opts: LadoRosto()),
            Q("B3", "Algo que prefere evitar nas imagens?", QuestionType.Chips, BriefingBlock.B, false, true, opts: EvitarChips()),
            Q("B4", "Nivel de conforto diante da camera. 1 travo → 5 adoro posar", QuestionType.Scale, BriefingBlock.B, false, false),
            Q("B5", "Prefere ser dirigido ou fotografado espontaneamente? 1 dirigido → 5 documental", QuestionType.Scale, BriefingBlock.B, false, false),
            Q("B6", "Enquadramento preferido", QuestionType.SingleChoice, BriefingBlock.B, false, false, opts: Enquadramento()),
            Q("B7", "Usa oculos? Prefere manter nas fotos?", QuestionType.SingleChoice, BriefingBlock.B, false, false, opts: Oculos()),
            Q("B8", "Qual foto sua voce mais gosta e por que?", QuestionType.Upload, BriefingBlock.B, false, false),
            Q("B9", "Tem alguma foto sua de que voce nao gostou? O que incomodou?", QuestionType.Text, BriefingBlock.B, false, true),
            Q("B10", "Como prefere ser chamado(a) durante o ensaio?", QuestionType.Text, BriefingBlock.B, false, false),
        ];

    private static IEnumerable<QuestionDef> BlockCFull() =>
        [
            Q("C1", "Fotos obrigatorias — lista nominal", QuestionType.List, BriefingBlock.C, false, false),
            Q("C2", "Alguem que exige atencao especial?", QuestionType.Text, BriefingBlock.C, false, true),
            Q("C3", "Ha pessoas que nao devem aparecer juntas na mesma foto?", QuestionType.Text, BriefingBlock.C, false, true),
            Q("C4", "Alguem nao autoriza uso de imagem?", QuestionType.MultiChoice, BriefingBlock.C, false, true),
            Q("C5", "Ha algo da historia que queira registrar?", QuestionType.Text, BriefingBlock.C, false, false),
        ];

    private static IEnumerable<QuestionDef> BlockD() =>
        [
            Q("D1", "Referencias visuais (links)", QuestionType.Url, BriefingBlock.D, false, false),
            Q("D2", "Upload de referencia", QuestionType.Upload, BriefingBlock.D, false, false),
            Q("D3", "Cores ou estilos a evitar", QuestionType.Text, BriefingBlock.D, false, false),
            Q("D4", "Autoriza uso de imagem em portfolio?", QuestionType.SingleChoice, BriefingBlock.D, true, false, opts: YesNo()),
        ];

    private static IEnumerable<QuestionDef> BlockE() =>
        [
            Q("E1", "Algo mais que o fotografo precise saber?", QuestionType.Text, BriefingBlock.E, false, false),
            Q("E4", "Canal preferido para alinhamentos", QuestionType.SingleChoice, BriefingBlock.E, false, false, opts: Canal()),
        ];

    private static QuestionDef Q(
        string code,
        string prompt,
        QuestionType type,
        BriefingBlock block,
        bool required,
        bool sensitive,
        string? visibleWhen = null,
        Opt[]? opts = null
    ) =>
        new(
            code,
            prompt,
            type,
            block,
            SortFromCode(code),
            required,
            sensitive,
            visibleWhen,
            opts ?? []
        );

    private static int SortFromCode(string code)
    {
        var letter = code[0] - 'A';
        var number = int.TryParse(code[1..], out var n) ? n : 0;
        return letter * 100 + number;
    }

    private static Opt[] YesNo() => [new("SIM", "Sim", 1), new("NAO", "Nao", 2)];

    private static Opt[] Ambiente() =>
        [new("INTERNO", "Interno", 1), new("EXTERNO", "Externo", 2), new("AMBOS", "Os dois", 3), new("INDEFINIDO", "Ainda nao definido", 4)];

    private static Opt[] Extras() =>
        [
            new("DRONE", "Drone", 1),
            new("SEGUNDO", "Segundo fotografo", 2),
            new("VIDEO", "Video", 3),
            new("ALBUM", "Album impresso", 4),
            new("MAKING", "Making of", 5),
            new("HORA", "Hora extra", 6),
        ];

    private static Opt[] LadoRosto() =>
        [new("ESQ", "Esquerdo", 1), new("DIR", "Direito", 2), new("NAO_SEI", "Nao sei", 3), new("TANTO_FAZ", "Tanto faz", 4)];

    private static Opt[] Enquadramento() =>
        [new("CORPO", "Corpo inteiro", 1), new("MEIO", "Meio corpo", 2), new("CLOSE", "Close", 3), new("CONFIO", "Confio no fotografo", 4)];

    private static Opt[] Oculos() =>
        [new("SIM_MANTER", "Sim, manter", 1), new("SIM_TIRAR", "Sim, preferir sem", 2), new("NAO", "Nao uso", 3)];

    private static Opt[] EvitarChips() =>
        [
            new("DENTES", "Sorriso mostrando os dentes", 1, true),
            new("BAIXO", "Angulo de baixo", 2, true),
            new("PERFIL", "Perfil", 3, true),
            new("BRACOS", "Bracos", 4, true),
            new("BARRIGA", "Barriga", 5, true),
        ];

    private static Opt[] Canal() => [new("WHATSAPP", "WhatsApp", 1), new("EMAIL", "E-mail", 2)];

    private sealed record Opt(string Code, string Label, int SortOrder, bool Chip = false);

    private sealed record QuestionDef(
        string Code,
        string Prompt,
        QuestionType Type,
        BriefingBlock Block,
        int SortOrder,
        bool IsRequired,
        bool IsSensitive,
        string? VisibleWhen,
        Opt[] Options
    );
}
