using System.Text.RegularExpressions;
using Prata.Domain.Common;

namespace Prata.Domain.Sales;

public enum PreferredChannel
{
    Email = 0,
    WhatsApp = 1,
}

/// <summary>
/// Cliente do estudio. Identificado por (tenant_id, email) — RN-COM-012.
/// Unicidade e enforceada na persistencia; o agregado so normaliza o e-mail.
/// </summary>
public sealed class Client : AggregateRoot, ITenantOwned
{
    private static readonly Regex EmailPattern = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant
    );

    private Client()
    {
        Name = null!;
        Email = null!;
    }

    private Client(
        Guid id,
        Guid tenantId,
        string name,
        string email,
        string? whatsapp,
        PreferredChannel preferredChannel
    )
        : base(id)
    {
        TenantId = tenantId;
        Name = name;
        Email = email;
        WhatsApp = whatsapp;
        PreferredChannel = preferredChannel;
    }

    public Guid TenantId { get; }

    public string Name { get; private set; }

    public string Email { get; private set; }

    public string? WhatsApp { get; private set; }

    public PreferredChannel PreferredChannel { get; private set; }

    public static Result<Client> Create(
        Guid tenantId,
        string name,
        string email,
        string? whatsapp,
        PreferredChannel preferredChannel
    )
    {
        if (tenantId == Guid.Empty)
            return SalesErrors.TenantInvalido;

        if (string.IsNullOrWhiteSpace(name))
            return SalesErrors.ClienteNomeObrigatorio;

        var normalized = NormalizeEmail(email);
        if (normalized is null)
            return SalesErrors.ClienteEmailInvalido;

        var phone = string.IsNullOrWhiteSpace(whatsapp) ? null : whatsapp.Trim();

        return new Client(Guid.NewGuid(), tenantId, name.Trim(), normalized, phone, preferredChannel);
    }

    public Result<Unit> AtualizarContato(string name, string? whatsapp, PreferredChannel preferredChannel)
    {
        if (string.IsNullOrWhiteSpace(name))
            return SalesErrors.ClienteNomeObrigatorio;

        Name = name.Trim();
        WhatsApp = string.IsNullOrWhiteSpace(whatsapp) ? null : whatsapp.Trim();
        PreferredChannel = preferredChannel;
        return Unit.Value;
    }

    public static string? NormalizeEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return null;

        var trimmed = email.Trim().ToLowerInvariant();
        return EmailPattern.IsMatch(trimmed) ? trimmed : null;
    }
}
