using Prata.Domain.Common;

namespace Prata.Domain.Tenancy;

/// <summary>
/// Politica de senha e lockout (RN-TEN-010).
/// </summary>
public static class PasswordPolicy
{
    public const int MinLength = 10;
    public const int MaxFailedAttempts = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public static Result<Unit> Validate(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < MinLength)
        {
            return Error.Validation(
                "SENHA_CURTA",
                $"Senha deve ter no minimo {MinLength} caracteres."
            );
        }

        return Unit.Value;
    }
}

/// <summary>
/// Regras de equipe do tenant (RN-TEN-007).
/// </summary>
public static class TeamPolicy
{
    public static readonly Error UltimoOwner = new(
        "ULTIMO_OWNER",
        "Nao e permitido remover ou rebaixar o ultimo owner ativo do tenant."
    );

    /// <summary>
    /// Bloqueia remocao/rebaixamento quando o alvo e owner e so resta um owner ativo.
    /// </summary>
    public static Result<Unit> PodeRemoverOuRebaixarOwner(bool targetIsOwner, int activeOwnerCount)
    {
        if (targetIsOwner && activeOwnerCount <= 1)
        {
            return UltimoOwner;
        }

        return Unit.Value;
    }
}

public static class TenantRoles
{
    public const string Owner = "tenant.owner";
    public const string Staff = "tenant.staff";
    public const string Client = "client";
}
