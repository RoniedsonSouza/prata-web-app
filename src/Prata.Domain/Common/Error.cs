namespace Prata.Domain.Common;

/// <summary>
/// Código de erro de domínio em português, SCREAMING_SNAKE (ADR-0009).
/// </summary>
public readonly record struct Error(string Code, string Message)
{
    public static Error Validation(string code, string message) => new(code, message);

    public override string ToString() => $"{Code}: {Message}";
}
