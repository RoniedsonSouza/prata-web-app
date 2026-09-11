using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Prata.Application.Abstractions;
using Prata.Domain.Notifications;
using Prata.Infrastructure.Persistence;

namespace Prata.Infrastructure.Notifications;

public sealed class SmtpNotifier(
    IConfiguration configuration,
    ILogger<SmtpNotifier> logger,
    PrataDbContext db,
    IDateTimeProvider clock
) : INotifier
{
    public async Task SendEmailAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        await DeliverAsync(to, subject, body, tenantDisplayName: null, cancellationToken);
    }

    public async Task SendTransactionalEmailAsync(
        Guid tenantId,
        string to,
        string type,
        string idempotencyKey,
        string subject,
        string body,
        CancellationToken cancellationToken = default
    )
    {
        var recipient = to.Trim().ToLowerInvariant();
        var existing = await db.NotificationMessages.FirstOrDefaultAsync(
            n =>
                n.TenantId == tenantId
                && n.Recipient == recipient
                && n.Type == type
                && n.IdempotencyKey == idempotencyKey,
            cancellationToken
        );

        if (existing is { SentAt: not null })
        {
            logger.LogInformation(
                "Notificacao idempotente ignorada type={Type} key={Key}",
                type,
                idempotencyKey
            );
            return;
        }

        var message =
            existing
            ?? NotificationMessage.Create(tenantId, recipient, type, idempotencyKey, subject, clock.UtcNow).Value;

        if (existing is null)
            db.NotificationMessages.Add(message);

        try
        {
            var tenantName = await db
                .Tenants.AsNoTracking()
                .Where(t => t.Id == tenantId)
                .Select(t => t.Name)
                .FirstOrDefaultAsync(cancellationToken);
            await DeliverAsync(recipient, subject, body, tenantName, cancellationToken);
            message.MarcarEnviado(clock.UtcNow);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            message.MarcarFalha(ex.Message);
            logger.LogWarning(ex, "Falha SMTP type={Type} (sem corpo)", type);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task DeliverAsync(
        string to,
        string subject,
        string body,
        string? tenantDisplayName,
        CancellationToken cancellationToken
    )
    {
        var host = configuration["Smtp:Host"];
        if (string.IsNullOrWhiteSpace(host) || host == "CHANGE_ME")
        {
            logger.LogInformation("SMTP nao configurado; e-mail para {ToHash} ignorado", Hash(to));
            return;
        }

        // Nunca logar body (RN-BRF-031 / RN-LGP-005).
        logger.LogInformation("Enviando e-mail transacional para {ToHash}", Hash(to));

        using var client = new System.Net.Mail.SmtpClient(host)
        {
            Port = int.TryParse(configuration["Smtp:Port"], out var port) ? port : 587,
            EnableSsl = configuration.GetValue("Smtp:UseSsl", true),
            Credentials = new System.Net.NetworkCredential(
                configuration["Smtp:Username"],
                configuration["Smtp:Password"]
            ),
        };

        // Envelope do dominio da plataforma; From amigavel do tenant (RN-NOT-011).
        var envelope =
            configuration["Smtp:EnvelopeFrom"]
            ?? configuration["Smtp:From"]
            ?? "noreply@prata.app";
        var friendly =
            !string.IsNullOrWhiteSpace(tenantDisplayName)
                ? tenantDisplayName.Trim()
                : configuration["Smtp:FromDisplay"] ?? "Prata";

        using var message = new System.Net.Mail.MailMessage
        {
            From = new System.Net.Mail.MailAddress(envelope, friendly),
            Subject = subject,
            Body = body,
            IsBodyHtml = false,
        };
        message.To.Add(to);
        await client.SendMailAsync(message, cancellationToken);
    }

    private static string Hash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes)[..12];
    }
}

/// <summary>Token HMAC de TTL curto para download da ficha (RN-BRF-040).</summary>
public interface ISignedFichaService
{
    string CreateToken(Guid tenantId, Guid orderId, TimeSpan ttl);

    bool TryValidate(string token, out Guid tenantId, out Guid orderId);
}

public sealed class SignedFichaService(IConfiguration configuration, IDateTimeProvider clock) : ISignedFichaService
{
    public string CreateToken(Guid tenantId, Guid orderId, TimeSpan ttl)
    {
        var exp = clock.UtcNow.Add(ttl).ToUnixTimeSeconds();
        var payload = $"{tenantId:N}.{orderId:N}.{exp}";
        var sig = Sign(payload);
        return Base64Url($"{payload}.{sig}");
    }

    public bool TryValidate(string token, out Guid tenantId, out Guid orderId)
    {
        tenantId = Guid.Empty;
        orderId = Guid.Empty;
        try
        {
            var raw = Encoding.UTF8.GetString(Base64UrlDecode(token));
            var parts = raw.Split('.');
            if (parts.Length != 4)
                return false;

            var payload = $"{parts[0]}.{parts[1]}.{parts[2]}";
            if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(Sign(payload)),
                    Encoding.UTF8.GetBytes(parts[3])
                ))
                return false;

            if (!long.TryParse(parts[2], out var exp) || clock.UtcNow.ToUnixTimeSeconds() > exp)
                return false;

            tenantId = Guid.Parse(parts[0]);
            orderId = Guid.Parse(parts[1]);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    private string Sign(string payload)
    {
        var key = configuration["Auth:Jwt:SigningKey"] ?? "DEV_ONLY_CHANGE_ME_32CHARS_MINIMUM!!";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)));
    }

    private static string Base64Url(string value) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(value)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string value)
    {
        var s = value.Replace('-', '+').Replace('_', '/');
        switch (s.Length % 4)
        {
            case 2:
                s += "==";
                break;
            case 3:
                s += "=";
                break;
        }

        return Convert.FromBase64String(s);
    }
}
