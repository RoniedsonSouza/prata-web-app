using Prata.Domain.Common;

namespace Prata.Domain.Briefing;

/// <summary>Os dez tipos de docs/07 §3.</summary>
public enum QuestionType
{
    Text = 0,
    SingleChoice = 1,
    MultiChoice = 2,
    Chips = 3,
    Scale = 4,
    Date = 5,
    Number = 6,
    Url = 7,
    Upload = 8,
    List = 9,
}

public enum BriefingBlock
{
    A = 0,
    B = 1,
    C = 2,
    D = 3,
    E = 4,
}

public static class BriefingErrors
{
    public static readonly Error TemplateNomeObrigatorio = new(
        "BRIEFING_TEMPLATE_NOME_OBRIGATORIO",
        "Nome do template e obrigatorio."
    );

    public static readonly Error PerguntaTextoObrigatorio = new(
        "BRIEFING_PERGUNTA_TEXTO_OBRIGATORIO",
        "Texto da pergunta e obrigatorio."
    );

    public static readonly Error VisibleWhenCircular = new(
        "BRIEFING_VISIBLE_WHEN_CIRCULAR",
        "VisibleWhen nao pode formar ciclo nem referenciar pergunta futura."
    );

    public static readonly Error VisibleWhenInvalido = new(
        "BRIEFING_VISIBLE_WHEN_INVALIDO",
        "VisibleWhen so pode referenciar pergunta anterior no mesmo template."
    );

    public static readonly Error RespostaTipoDivergente = new(
        "BRIEFING_RESPOSTA_TIPO_DIVERGENTE",
        "Valor da resposta nao corresponde ao QuestionType."
    );

    public static readonly Error ConsentimentoAusente = new(
        "BRIEFING_CONSENTIMENTO_AUSENTE",
        "Bloco sensivel exige consentimento registrado."
    );

    public static readonly Error DiagnosticoProibido = new(
        "BRIEFING_DIAGNOSTICO_PROIBIDO",
        "Pergunta sensivel nao pode pedir causa/diagnostico (RN-BRF-021)."
    );

    public static readonly Error TemplateJaPublicado = new(
        "BRIEFING_TEMPLATE_JA_PUBLICADO",
        "Template ja esta publicado."
    );
}

/// <summary>
/// Template versionado por (tenant, ServiceType). Copia da semente — RN-BRF-001.
/// </summary>
public sealed class BriefingTemplate : AggregateRoot, ITenantOwned
{
    private readonly List<Question> _questions = [];

    private BriefingTemplate()
    {
        Name = null!;
    }

    private BriefingTemplate(Guid id, Guid tenantId, Guid serviceTypeId, string name, int version)
        : base(id)
    {
        TenantId = tenantId;
        ServiceTypeId = serviceTypeId;
        Name = name;
        Version = version;
        IsPublished = false;
    }

    public Guid TenantId { get; }

    public Guid ServiceTypeId { get; }

    public string Name { get; private set; }

    public int Version { get; private set; }

    public bool IsPublished { get; private set; }

    public IReadOnlyList<Question> Questions => _questions;

    public static Result<BriefingTemplate> Create(Guid tenantId, Guid serviceTypeId, string name)
    {
        if (tenantId == Guid.Empty || serviceTypeId == Guid.Empty)
            return Error.Validation("BRIEFING_TENANT_INVALIDO", "Tenant e ServiceType sao obrigatorios.");

        if (string.IsNullOrWhiteSpace(name))
            return BriefingErrors.TemplateNomeObrigatorio;

        return new BriefingTemplate(Guid.NewGuid(), tenantId, serviceTypeId, name.Trim(), version: 1);
    }

    public Result<Unit> AdicionarPergunta(Question question)
    {
        if (IsPublished)
            return BriefingErrors.TemplateJaPublicado;

        if (question.TenantId != TenantId || question.TemplateId != Id)
            return Error.Validation("BRIEFING_PERGUNTA_TEMPLATE", "Pergunta nao pertence a este template.");

        _questions.Add(question);
        return Unit.Value;
    }

    public Result<Unit> Publicar()
    {
        if (IsPublished)
            return BriefingErrors.TemplateJaPublicado;

        var codes = _questions.OrderBy(q => q.SortOrder).Select(q => q.Code).ToList();
        foreach (var q in _questions)
        {
            var check = VisibleWhen.Validate(q.VisibleWhen, q.Code, codes);
            if (check.IsFailure)
                return check;

            if (q.IsSensitive && LooksLikeDiagnosis(q.Prompt))
                return BriefingErrors.DiagnosticoProibido;
        }

        IsPublished = true;
        return Unit.Value;
    }

    public Result<Unit> AbrirEdicao()
    {
        IsPublished = false;
        return Unit.Value;
    }

    public Result<Unit> RemoverPergunta(string code)
    {
        if (IsPublished)
            return BriefingErrors.TemplateJaPublicado;

        var normalized = code.Trim().ToUpperInvariant();
        var removed = _questions.RemoveAll(q => q.Code == normalized);
        if (removed == 0)
            return Error.Validation("BRIEFING_PERGUNTA_AUSENTE", "Pergunta nao encontrada.");

        return Unit.Value;
    }

    /// <summary>Cria rascunho Version+1 copiando perguntas (RN-BRF-001: copia, nao referencia).</summary>
    public Result<BriefingTemplate> CriarNovaVersao()
    {
        var next = new BriefingTemplate(
            Guid.NewGuid(),
            TenantId,
            ServiceTypeId,
            Name,
            Version + 1
        );

        foreach (var q in _questions.OrderBy(x => x.SortOrder))
        {
            var copy = Question
                .Create(
                    TenantId,
                    next.Id,
                    q.Code,
                    q.Prompt,
                    q.Type,
                    q.Block,
                    q.SortOrder,
                    q.IsRequired,
                    q.IsSensitive,
                    q.VisibleWhen
                )
                .Value;
            foreach (var opt in q.Options)
                copy.AdicionarOpcao(opt.Code, opt.Label, opt.SortOrder, opt.IsSuggestedChip);
            next.AdicionarPergunta(copy);
        }

        return next;
    }

    private static bool LooksLikeDiagnosis(string prompt)
    {
        var lower = prompt.ToLowerInvariant();
        return lower.Contains("diagnostico", StringComparison.Ordinal)
            || lower.Contains("doença", StringComparison.Ordinal)
            || lower.Contains("doenca", StringComparison.Ordinal)
            || lower.Contains("por que voce tem", StringComparison.Ordinal)
            || lower.Contains("qual a causa", StringComparison.Ordinal);
    }
}

public sealed class Question : Entity, ITenantOwned
{
    private readonly List<QuestionOption> _options = [];

    private Question()
    {
        Code = null!;
        Prompt = null!;
    }

    private Question(
        Guid id,
        Guid tenantId,
        Guid templateId,
        string code,
        string prompt,
        QuestionType type,
        BriefingBlock block,
        int sortOrder,
        bool isRequired,
        bool isSensitive,
        string? visibleWhen
    )
        : base(id)
    {
        TenantId = tenantId;
        TemplateId = templateId;
        Code = code;
        Prompt = prompt;
        Type = type;
        Block = block;
        SortOrder = sortOrder;
        IsRequired = isRequired;
        IsSensitive = isSensitive;
        VisibleWhen = visibleWhen;
    }

    public Guid TenantId { get; }

    public Guid TemplateId { get; }

    public string Code { get; }

    public string Prompt { get; }

    public QuestionType Type { get; }

    public BriefingBlock Block { get; }

    public int SortOrder { get; }

    public bool IsRequired { get; }

    public bool IsSensitive { get; }

    public string? VisibleWhen { get; }

    public IReadOnlyList<QuestionOption> Options => _options;

    public static Result<Question> Create(
        Guid tenantId,
        Guid templateId,
        string code,
        string prompt,
        QuestionType type,
        BriefingBlock block,
        int sortOrder,
        bool isRequired,
        bool isSensitive,
        string? visibleWhen = null
    )
    {
        if (string.IsNullOrWhiteSpace(code))
            return Error.Validation("BRIEFING_PERGUNTA_CODIGO", "Codigo da pergunta e obrigatorio.");

        if (string.IsNullOrWhiteSpace(prompt))
            return BriefingErrors.PerguntaTextoObrigatorio;

        return new Question(
            Guid.NewGuid(),
            tenantId,
            templateId,
            code.Trim().ToUpperInvariant(),
            prompt.Trim(),
            type,
            block,
            sortOrder,
            isRequired,
            isSensitive,
            string.IsNullOrWhiteSpace(visibleWhen) ? null : visibleWhen.Trim()
        );
    }

    public Result<Unit> AdicionarOpcao(string optionCode, string label, int sortOrder, bool isSuggestedChip = false)
    {
        if (string.IsNullOrWhiteSpace(optionCode) || string.IsNullOrWhiteSpace(label))
            return Error.Validation("BRIEFING_OPCAO_INVALIDA", "Opcao exige codigo e rotulo.");

        _options.Add(new QuestionOption(optionCode.Trim().ToUpperInvariant(), label.Trim(), sortOrder, isSuggestedChip));
        return Unit.Value;
    }
}

public sealed record QuestionOption(string Code, string Label, int SortOrder, bool IsSuggestedChip);

/// <summary>
/// Expressao simples: "B1" ou "B1.value >= 3". Validacao de grafo em Publicar.
/// </summary>
public static class VisibleWhen
{
    public static Result<Unit> Validate(string? expression, string currentCode, IReadOnlyList<string> orderedCodes)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return Unit.Value;

        var referenced = ExtractCodes(expression);
        var currentIndex = IndexOfCode(orderedCodes, currentCode);
        if (currentIndex < 0)
            return BriefingErrors.VisibleWhenInvalido;

        foreach (var code in referenced)
        {
            var refIndex = IndexOfCode(orderedCodes, code);
            if (refIndex < 0)
                return BriefingErrors.VisibleWhenInvalido;
            if (refIndex >= currentIndex)
                return BriefingErrors.VisibleWhenCircular;
        }

        return Unit.Value;
    }

    private static int IndexOfCode(IReadOnlyList<string> codes, string code)
    {
        for (var i = 0; i < codes.Count; i++)
        {
            if (string.Equals(codes[i], code, StringComparison.Ordinal))
                return i;
        }

        return -1;
    }

    public static IReadOnlyList<string> ExtractCodes(string expression)
    {
        var codes = new List<string>();
        foreach (var token in expression.Split([' ', '.', '>', '<', '=', '!', '&', '|', '(', ')'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (token.Length >= 2 && char.IsLetter(token[0]) && token.Skip(1).All(char.IsLetterOrDigit))
            {
                var upper = token.ToUpperInvariant();
                if (upper is "VALUE" or "NUMBER" or "OPTION" or "AND" or "OR" or "TRUE" or "FALSE")
                    continue;
                if (!codes.Contains(upper, StringComparer.Ordinal))
                    codes.Add(upper);
            }
        }

        return codes;
    }
}

/// <summary>
/// Resposta com snapshot do texto/tipo (RN-BRF-002).
/// A forma do jsonb e validada na Application (Domain nao referencia System.Text.Json).
/// </summary>
public sealed class Answer : Entity, ITenantOwned
{
    private Answer()
    {
        QuestionCode = null!;
        PromptSnapshot = null!;
        ValueJson = null!;
    }

    private Answer(
        Guid id,
        Guid tenantId,
        Guid orderId,
        string questionCode,
        string promptSnapshot,
        QuestionType typeSnapshot,
        bool isSensitive,
        string valueJson
    )
        : base(id)
    {
        TenantId = tenantId;
        OrderId = orderId;
        QuestionCode = questionCode;
        PromptSnapshot = promptSnapshot;
        TypeSnapshot = typeSnapshot;
        IsSensitive = isSensitive;
        ValueJson = valueJson;
    }

    public Guid TenantId { get; }

    public Guid OrderId { get; }

    public string QuestionCode { get; }

    public string PromptSnapshot { get; }

    public QuestionType TypeSnapshot { get; }

    public bool IsSensitive { get; }

    public string ValueJson { get; }

    public static Result<Answer> Create(
        Guid tenantId,
        Guid orderId,
        Question question,
        string valueJson,
        bool valueShapeValid,
        bool hasSensitiveConsent
    )
    {
        if (question.IsSensitive && question.Block == BriefingBlock.B && !hasSensitiveConsent)
            return BriefingErrors.ConsentimentoAusente;

        if (!valueShapeValid)
            return BriefingErrors.RespostaTipoDivergente;

        if (string.IsNullOrWhiteSpace(valueJson))
            return BriefingErrors.RespostaTipoDivergente;

        return new Answer(
            Guid.NewGuid(),
            tenantId,
            orderId,
            question.Code,
            question.Prompt,
            question.Type,
            question.IsSensitive,
            valueJson
        );
    }
}

/// <summary>Consentimento do bloco sensivel com snapshot da finalidade (RN-BRF-020 / RN-LGP-003).</summary>
public sealed class BriefingConsent : Entity, ITenantOwned
{
    private BriefingConsent()
    {
        Scope = null!;
        PurposeTextSnapshot = null!;
        Ip = null!;
        UserAgent = null!;
    }

    private BriefingConsent(
        Guid id,
        Guid tenantId,
        Guid orderId,
        string scope,
        DateTimeOffset consentedAt,
        string ip,
        string userAgent,
        string purposeTextSnapshot
    )
        : base(id)
    {
        TenantId = tenantId;
        OrderId = orderId;
        Scope = scope;
        ConsentedAt = consentedAt;
        Ip = ip;
        UserAgent = userAgent;
        PurposeTextSnapshot = purposeTextSnapshot;
        RevokedAt = null;
    }

    public Guid TenantId { get; }

    public Guid OrderId { get; }

    public string Scope { get; }

    public DateTimeOffset ConsentedAt { get; }

    public string Ip { get; }

    public string UserAgent { get; }

    public string PurposeTextSnapshot { get; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public bool IsActive => RevokedAt is null;

    public static Result<BriefingConsent> Create(
        Guid tenantId,
        Guid orderId,
        DateTimeOffset agora,
        string ip,
        string userAgent,
        string purposeTextSnapshot
    )
    {
        if (string.IsNullOrWhiteSpace(purposeTextSnapshot))
            return Error.Validation("BRIEFING_FINALIDADE_OBRIGATORIA", "Snapshot da finalidade e obrigatorio.");

        return new BriefingConsent(
            Guid.NewGuid(),
            tenantId,
            orderId,
            scope: "bloco-B",
            agora,
            ip.Trim(),
            userAgent.Trim(),
            purposeTextSnapshot.Trim()
        );
    }

    public Result<Unit> Revogar(DateTimeOffset agora)
    {
        if (RevokedAt is not null)
            return Error.Validation("BRIEFING_CONSENTIMENTO_JA_REVOGADO", "Consentimento ja revogado.");

        RevokedAt = agora;
        return Unit.Value;
    }
}
