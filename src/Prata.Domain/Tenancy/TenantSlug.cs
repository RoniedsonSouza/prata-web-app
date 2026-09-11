using System.Text.RegularExpressions;
using Prata.Domain.Common;

namespace Prata.Domain.Tenancy;

/// <summary>
/// Slug de tenant / subdominio. RN-TEN-003.
/// </summary>
public sealed partial class TenantSlug : ValueObject
{
    public const int MinLength = 3;
    public const int MaxLength = 40;

    /// <summary>
    /// Subdominios reservados da plataforma (docs/03-MULTI-TENANCY.md §2).
    /// </summary>
    public static readonly IReadOnlySet<string> Reserved = new HashSet<string>(StringComparer.Ordinal)
    {
        "www",
        "api",
        "app",
        "admin",
        "auth",
        "login",
        "static",
        "assets",
        "cdn",
        "img",
        "media",
        "mail",
        "smtp",
        "ftp",
        "ns1",
        "ns2",
        "mx",
        "blog",
        "help",
        "suporte",
        "status",
        "docs",
        "s",
        "t",
        "webhook",
        "webhooks",
        "painel",
        "console",
        "plataforma",
        "prata",
    };

    private TenantSlug(string value) => Value = value;

    public string Value { get; }

    public static Result<TenantSlug> Create(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return TenancyErrors.SlugInvalido;

        foreach (var c in raw)
        {
            if (char.IsAsciiLetterUpper(c))
                return TenancyErrors.SlugInvalido;
        }

        var value = raw.Trim();

        if (value.Length is < MinLength or > MaxLength)
            return TenancyErrors.SlugInvalido;

        if (!SlugRegex().IsMatch(value))
            return TenancyErrors.SlugInvalido;

        if (Reserved.Contains(value))
            return TenancyErrors.SlugReservado;

        return new TenantSlug(value);
    }

    public override string ToString() => Value;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    [GeneratedRegex("^[a-z0-9](?:[a-z0-9-]{1,38}[a-z0-9])$", RegexOptions.CultureInvariant)]
    private static partial Regex SlugRegex();
}
