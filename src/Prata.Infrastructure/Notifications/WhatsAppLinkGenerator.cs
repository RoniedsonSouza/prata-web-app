namespace Prata.Infrastructure.Notifications;

/// <summary>Gera link wa.me pre-preenchido — so disparado por acao humana (RN-NOT-010, ADR-0007).</summary>
public interface IWhatsAppLinkGenerator
{
    string CreatePrefilledLink(string phoneE164OrDigits, string message);
}

public sealed class WhatsAppLinkGenerator : IWhatsAppLinkGenerator
{
    public string CreatePrefilledLink(string phoneE164OrDigits, string message)
    {
        var digits = new string(phoneE164OrDigits.Where(char.IsDigit).ToArray());
        var text = Uri.EscapeDataString(message);
        return $"https://wa.me/{digits}?text={text}";
    }
}
