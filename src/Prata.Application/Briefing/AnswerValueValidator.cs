using System.Text.Json;
using Prata.Domain.Briefing;
using Prata.Domain.Common;

namespace Prata.Application.Briefing;

/// <summary>Valida a forma jsonb do valor pelo QuestionType (RN-BRF-004). Fora do Domain.</summary>
public static class AnswerValueValidator
{
    public static Result<Unit> Validate(QuestionType type, string valueJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(valueJson);
            var value = doc.RootElement;
            return type switch
            {
                QuestionType.Text => RequireString(value, "text"),
                QuestionType.SingleChoice => RequireString(value, "option"),
                QuestionType.MultiChoice => RequireArray(value, "options"),
                QuestionType.Chips => RequireChips(value),
                QuestionType.Scale => RequireScale(value),
                QuestionType.Date => RequireString(value, "date"),
                QuestionType.Number => RequireNumber(value, "number"),
                QuestionType.Url => RequireArray(value, "urls"),
                QuestionType.Upload => RequireArray(value, "assets"),
                QuestionType.List => RequireArray(value, "items"),
                _ => BriefingErrors.RespostaTipoDivergente,
            };
        }
        catch (JsonException)
        {
            return BriefingErrors.RespostaTipoDivergente;
        }
    }

    private static Result<Unit> RequireString(JsonElement value, string prop)
    {
        if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(prop, out var p) || p.ValueKind != JsonValueKind.String)
            return BriefingErrors.RespostaTipoDivergente;
        return Unit.Value;
    }

    private static Result<Unit> RequireNumber(JsonElement value, string prop)
    {
        if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(prop, out var p) || p.ValueKind != JsonValueKind.Number)
            return BriefingErrors.RespostaTipoDivergente;
        return Unit.Value;
    }

    private static Result<Unit> RequireArray(JsonElement value, string prop)
    {
        if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(prop, out var p) || p.ValueKind != JsonValueKind.Array)
            return BriefingErrors.RespostaTipoDivergente;
        return Unit.Value;
    }

    private static Result<Unit> RequireChips(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object)
            return BriefingErrors.RespostaTipoDivergente;
        if (!value.TryGetProperty("chips", out var chips) || chips.ValueKind != JsonValueKind.Array)
            return BriefingErrors.RespostaTipoDivergente;
        return Unit.Value;
    }

    private static Result<Unit> RequireScale(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object)
            return BriefingErrors.RespostaTipoDivergente;
        if (!value.TryGetProperty("value", out var v) || v.ValueKind != JsonValueKind.Number)
            return BriefingErrors.RespostaTipoDivergente;
        return Unit.Value;
    }
}
