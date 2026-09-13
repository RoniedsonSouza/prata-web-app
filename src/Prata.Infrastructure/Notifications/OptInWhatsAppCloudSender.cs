using Prata.Application.Abstractions;
using Prata.Domain.Common;
using Prata.Domain.Tenancy;
using Prata.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Prata.Infrastructure.Notifications;

/// <summary>Stub Cloud API: so envia se tenant tiver WhatsAppCloudEnabled (RN-NOT-012).</summary>
public sealed class OptInWhatsAppCloudSender(PrataDbContext db) : IWhatsAppCloudSender
{
    public async Task<Result<Unit>> SendTemplateAsync(
        Guid tenantId,
        string phoneE164,
        string templateName,
        IReadOnlyDictionary<string, string> parameters,
        CancellationToken cancellationToken = default
    )
    {
        _ = (phoneE164, templateName, parameters);
        var tenant = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);
        if (tenant is null || !tenant.WhatsAppCloudEnabled)
            return Error.Validation(
                "WHATSAPP_CLOUD_DESLIGADO",
                "Cloud API desligada neste tenant; use wa.me (RN-NOT-010/012)."
            );

        // Integracao real WABA fica para configuracao por tenant; port pronto.
        return Unit.Value;
    }
}
