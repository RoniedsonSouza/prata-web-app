using Prata.Domain.Common;

namespace Prata.Domain.Tenancy;

/// <summary>
/// Familia de refresh tokens. Reuso de token ja rotacionado invalida a familia
/// (docs/12-SEGURANCA-E-LGPD.md §1).
/// </summary>
public sealed class RefreshTokenFamily : AggregateRoot, ITenantOwned
{
    private RefreshTokenFamily()
    {
        CurrentTokenHash = null!;
    }

    private RefreshTokenFamily(
        Guid id,
        Guid tenantId,
        Guid userId,
        string currentTokenHash,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt
    )
        : base(id)
    {
        TenantId = tenantId;
        UserId = userId;
        CurrentTokenHash = currentTokenHash;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
        IsRevoked = false;
    }

    public Guid TenantId { get; private set; }

    public Guid UserId { get; private set; }

    public string CurrentTokenHash { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public bool IsRevoked { get; private set; }

    public static RefreshTokenFamily Create(
        Guid tenantId,
        Guid userId,
        string tokenHash,
        DateTimeOffset agora,
        TimeSpan lifetime
    ) => Create(Guid.NewGuid(), tenantId, userId, tokenHash, agora, lifetime);

    public static RefreshTokenFamily Create(
        Guid id,
        Guid tenantId,
        Guid userId,
        string tokenHash,
        DateTimeOffset agora,
        TimeSpan lifetime
    ) => new(id, tenantId, userId, tokenHash, agora, agora.Add(lifetime));

    public Result<Unit> Rotate(string presentedHash, string newHash, DateTimeOffset agora)
    {
        if (IsRevoked)
        {
            return Error.Validation("REFRESH_REVOGADO", "Familia de refresh token revogada.");
        }

        if (agora > ExpiresAt)
        {
            IsRevoked = true;
            return Error.Validation("REFRESH_EXPIRADO", "Refresh token expirado.");
        }

        if (!string.Equals(CurrentTokenHash, presentedHash, StringComparison.Ordinal))
        {
            // Reuso de token antigo = indício de roubo → invalida familia.
            IsRevoked = true;
            return Error.Validation(
                "REFRESH_REUSADO",
                "Refresh token reutilizado; sessao invalidada."
            );
        }

        CurrentTokenHash = newHash;
        return Unit.Value;
    }

    public void Revoke() => IsRevoked = true;
}
