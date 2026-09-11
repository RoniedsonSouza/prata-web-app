using System.Text.Json;
using Prata.Domain.Briefing;

namespace Prata.Application.Briefing;

/// <summary>
/// Avaliacao de VisibleWhen no servidor (espelha o client).
/// Expressoes suportadas: "B1", "B1.value >= 3", "B2.option == NAO_SEI".
/// </summary>
public static class VisibleWhenEvaluator
{
    public static bool IsVisible(Question question, IReadOnlyDictionary<string, Answer> answers)
    {
        if (string.IsNullOrWhiteSpace(question.VisibleWhen))
            return true;

        var expression = question.VisibleWhen.Trim();
        var refs = VisibleWhen.ExtractCodes(expression);
        if (refs.Count == 0)
            return true;

        if (!refs.All(answers.ContainsKey))
            return false;

        // So codigo: visivel se a referencia existe.
        if (refs.Count == 1 && string.Equals(expression, refs[0], StringComparison.OrdinalIgnoreCase))
            return true;

        // Comparacao numerica: CODE.value OP N
        var parts = expression.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length >= 3 && parts[0].Contains(".value", StringComparison.OrdinalIgnoreCase))
        {
            var code = parts[0].Split('.')[0].ToUpperInvariant();
            if (!answers.TryGetValue(code, out var answer))
                return false;
            if (!TryReadNumber(answer.ValueJson, out var left))
                return false;
            if (!decimal.TryParse(parts[^1], out var right))
                return false;
            var op = parts.Length == 3 ? parts[1] : string.Join(' ', parts.Skip(1).Take(parts.Length - 2));
            return op switch
            {
                ">=" => left >= right,
                "<=" => left <= right,
                ">" => left > right,
                "<" => left < right,
                "==" or "=" => left == right,
                "!=" => left != right,
                _ => true,
            };
        }

        // Comparacao de opcao: CODE.option == X
        if (parts.Length >= 3 && parts[0].Contains(".option", StringComparison.OrdinalIgnoreCase))
        {
            var code = parts[0].Split('.')[0].ToUpperInvariant();
            if (!answers.TryGetValue(code, out var answer))
                return false;
            var expected = parts[^1].Trim('"', '\'');
            var actual = ReadOption(answer.ValueJson);
            var op = parts[1];
            return op is "==" or "="
                ? string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase)
                : !string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
        }

        return true;
    }

    private static bool TryReadNumber(string valueJson, out decimal value)
    {
        value = 0;
        try
        {
            using var doc = JsonDocument.Parse(valueJson);
            var root = doc.RootElement;
            if (root.TryGetProperty("value", out var v))
            {
                if (v.ValueKind == JsonValueKind.Number)
                {
                    value = v.GetDecimal();
                    return true;
                }
                if (v.ValueKind == JsonValueKind.String && decimal.TryParse(v.GetString(), out value))
                    return true;
            }
            if (root.TryGetProperty("number", out var n) && n.TryGetDecimal(out value))
                return true;
        }
        catch (JsonException)
        {
            return false;
        }

        return false;
    }

    private static string? ReadOption(string valueJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(valueJson);
            if (doc.RootElement.TryGetProperty("option", out var o))
                return o.GetString();
            if (doc.RootElement.TryGetProperty("value", out var v) && v.ValueKind == JsonValueKind.String)
                return v.GetString();
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }
}
