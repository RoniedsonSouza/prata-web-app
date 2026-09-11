using Prata.Domain.Common;

namespace Prata.Domain.Tenancy;

public enum InviteStatus
{
    Pendente = 0,
    Aceito = 1,
    Expirado = 2,
    Revogado = 3,
}

public sealed record MembroConvidado(Guid TenantId, Guid InviteId, string Email, string Role) : DomainEvent;

public sealed record ConviteAceito(Guid TenantId, Guid InviteId, Guid UserId) : DomainEvent;

/// <summary>
/// Convite de equipe. Expira em 7 dias (RN-TEN-008).
/// </summary>
public sealed class Invite : AggregateRoot, ITenantOwned
{
    public static readonly TimeSpan Validity = TimeSpan.FromDays(7);

    private Invite()
    {
        Email = null!;
        Role = null!;
        TokenHash = null!;
    }

    private Invite(
        Guid id,
        Guid tenantId,
        string email,
        string role,
        string tokenHash,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt,
        InviteStatus status
    )
        : base(id)
    {
        TenantId = tenantId;
        Email = email;
        Role = role;
        TokenHash = tokenHash;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
        Status = status;
    }

    public Guid TenantId { get; }

    public string Email { get; }

    public string Role { get; }

    public string TokenHash { get; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset ExpiresAt { get; }

    public InviteStatus Status { get; private set; }

    public static Result<Invite> Create(Guid tenantId, string email, string role, string tokenHash, DateTimeOffset agora)
    {
        if (string.IsNullOrWhiteSpace(email))
            return Error.Validation("CONVITE_EMAIL_OBRIGATORIO", "E-mail do convite e obrigatorio.");

        if (string.IsNullOrWhiteSpace(role))
            return Error.Validation("CONVITE_PAPEL_OBRIGATORIO", "Papel do convite e obrigatorio.");

        if (string.IsNullOrWhiteSpace(tokenHash))
            return Error.Validation("CONVITE_TOKEN_OBRIGATORIO", "Token do convite e obrigatorio.");

        var invite = new Invite(
            Guid.NewGuid(),
            tenantId,
            email.Trim().ToLowerInvariant(),
            role.Trim(),
            tokenHash,
            agora,
            agora.Add(Validity),
            InviteStatus.Pendente
        );

        invite.Raise(new MembroConvidado(tenantId, invite.Id, invite.Email, invite.Role));
        return invite;
    }

    public Result<Unit> Aceitar(Guid userId, DateTimeOffset agora)
    {
        if (Status != InviteStatus.Pendente)
            return Error.Validation("CONVITE_NAO_PENDENTE", "Convite nao esta pendente.");

        if (agora > ExpiresAt)
        {
            Status = InviteStatus.Expirado;
            return Error.Validation("CONVITE_EXPIRADO", "Convite expirado.");
        }

        Status = InviteStatus.Aceito;
        Raise(new ConviteAceito(TenantId, Id, userId));
        return Unit.Value;
    }

    public Result<Unit> Revogar()
    {
        if (Status != InviteStatus.Pendente)
            return Error.Validation("CONVITE_NAO_PENDENTE", "Convite nao esta pendente.");

        Status = InviteStatus.Revogado;
        return Unit.Value;
    }

    public void MarcarExpiradoSeNecessario(DateTimeOffset agora)
    {
        if (Status == InviteStatus.Pendente && agora > ExpiresAt)
            Status = InviteStatus.Expirado;
    }
}
