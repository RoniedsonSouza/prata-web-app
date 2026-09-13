using Prata.Domain.Common;

namespace Prata.Domain.Notifications;

/// <summary>
/// Registro de tentativa de notificacao. Idempotencia por
/// (tenant, destinatario, tipo, chave) — RN-NOT-001.
/// </summary>
public sealed class NotificationMessage : Entity, ITenantOwned
{
    private NotificationMessage()
    {
        Recipient = null!;
        Type = null!;
        IdempotencyKey = null!;
        Subject = null!;
    }

    private NotificationMessage(
        Guid id,
        Guid tenantId,
        string recipient,
        string type,
        string idempotencyKey,
        string subject,
        DateTimeOffset createdAt
    )
        : base(id)
    {
        TenantId = tenantId;
        Recipient = recipient;
        Type = type;
        IdempotencyKey = idempotencyKey;
        Subject = subject;
        CreatedAt = createdAt;
    }

    public Guid TenantId { get; }

    public string Recipient { get; }

    public string Type { get; }

    public string IdempotencyKey { get; }

    public string Subject { get; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset? SentAt { get; private set; }

    public string? LastError { get; private set; }

    public static Result<NotificationMessage> Create(
        Guid tenantId,
        string recipient,
        string type,
        string idempotencyKey,
        string subject,
        DateTimeOffset agora
    )
    {
        if (tenantId == Guid.Empty)
            return Error.Validation("NOTIF_TENANT", "Tenant obrigatorio.");
        if (string.IsNullOrWhiteSpace(recipient))
            return Error.Validation("NOTIF_DESTINATARIO", "Destinatario obrigatorio.");
        if (string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(idempotencyKey))
            return Error.Validation("NOTIF_CHAVE", "Tipo e chave de idempotencia obrigatorios.");

        return new NotificationMessage(
            Guid.NewGuid(),
            tenantId,
            recipient.Trim().ToLowerInvariant(),
            type.Trim(),
            idempotencyKey.Trim(),
            subject.Trim(),
            agora
        );
    }

    public void MarcarEnviado(DateTimeOffset agora)
    {
        SentAt = agora;
        LastError = null;
    }

    public void MarcarFalha(string error)
    {
        // Nunca gravar corpo sensivel — so mensagem tecnica curta.
        LastError = error.Length > 500 ? error[..500] : error;
    }
}

public static class NotificationTypes
{
    public const string PedidoRecebido = "pedido_recebido";
    public const string OrcamentoEnviado = "orcamento_enviado";
    public const string OrcamentoExpirando = "orcamento_expirando";
    public const string OrcamentoAprovado = "orcamento_aprovado";
    public const string LembreteAssinatura = "lembrete_assinatura";
    public const string LembreteSaldo = "lembrete_saldo";
    public const string LembreteEvento = "lembrete_evento";
}
