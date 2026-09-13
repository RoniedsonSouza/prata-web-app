using Prata.Domain.Common;

namespace Prata.Application.Abstractions;

/// <summary>
/// Cloud API WhatsApp — opcional por tenant (RN-NOT-012, ADR-0007).
/// Sem opt-in, o fluxo continua em wa.me (RN-NOT-010).
/// </summary>
public interface IWhatsAppCloudSender
{
    Task<Result<Unit>> SendTemplateAsync(
        Guid tenantId,
        string phoneE164,
        string templateName,
        IReadOnlyDictionary<string, string> parameters,
        CancellationToken cancellationToken = default
    );
}
