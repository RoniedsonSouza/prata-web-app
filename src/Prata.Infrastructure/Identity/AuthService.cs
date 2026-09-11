using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Prata.Application.Abstractions;
using Prata.Domain.Catalog;
using Prata.Domain.Common;
using Prata.Domain.Tenancy;
using Prata.Infrastructure.Briefing;
using Prata.Infrastructure.Persistence;

namespace Prata.Infrastructure.Identity;

public sealed class JwtOptions
{
    public const string SectionName = "Auth:Jwt";

    public string Issuer { get; set; } = "prata";

    public string Audience { get; set; } = "prata";

    public string SigningKey { get; set; } = string.Empty;

    public int AccessTokenMinutes { get; set; } = 15;

    public int RefreshTokenDays { get; set; } = 30;
}

public sealed record AuthTokens(string AccessToken, string RefreshToken, DateTimeOffset AccessExpiresAt);

public interface IJwtTokenService
{
    AuthTokens IssueTokens(AppUser user, string role);

    string HashToken(string rawToken);
}

public sealed class JwtTokenService(IOptions<JwtOptions> options, IDateTimeProvider clock) : IJwtTokenService
{
    public AuthTokens IssueTokens(AppUser user, string role)
    {
        var opts = options.Value;
        if (string.IsNullOrWhiteSpace(opts.SigningKey) || opts.SigningKey.Length < 32)
        {
            throw new InvalidOperationException("Auth:Jwt:SigningKey deve ter ao menos 32 caracteres.");
        }

        var expires = clock.UtcNow.AddMinutes(opts.AccessTokenMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new("tenant_id", user.TenantId.ToString()),
            new(ClaimTypes.Role, role),
            new("role", role),
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(opts.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var jwt = new JwtSecurityToken(
            issuer: opts.Issuer,
            audience: opts.Audience,
            claims: claims,
            notBefore: clock.UtcNow.UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: credentials
        );

        var access = new JwtSecurityTokenHandler().WriteToken(jwt);
        var refresh = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        return new AuthTokens(access, refresh, expires);
    }

    public string HashToken(string rawToken)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(hash);
    }
}

public interface IAuthService
{
    Task<Result<AuthTokens>> RegisterStudioAsync(
        string studioName,
        string slug,
        string ownerName,
        string email,
        string password,
        CancellationToken ct
    );

    Task<Result<AuthTokens>> LoginAsync(string email, string password, CancellationToken ct);

    Task<Result<AuthTokens>> RefreshAsync(string refreshToken, CancellationToken ct);

    Task<Result<Unit>> RequestPasswordResetAsync(string email, CancellationToken ct);

    Task<Result<Unit>> ConfirmPasswordResetAsync(string token, string newPassword, CancellationToken ct);

    Task<Result<Guid>> InviteAsync(string email, string role, CancellationToken ct);

    Task<Result<AuthTokens>> AcceptInviteAsync(
        string inviteToken,
        string name,
        string password,
        CancellationToken ct
    );

    Task<Result<Unit>> RemoveTeamMemberAsync(Guid userId, CancellationToken ct);
}

public sealed class AuthService(
    UserManager<AppUser> users,
    RoleManager<AppRole> roles,
    SignInManager<AppUser> signIn,
    PrataDbContext db,
    ITenantContext tenantContext,
    ITenantContextAccessor tenantAccessor,
    IJwtTokenService tokens,
    IOptions<JwtOptions> jwtOptions,
    IDateTimeProvider clock,
    INotifier notifier
) : IAuthService
{
    public async Task<Result<AuthTokens>> RegisterStudioAsync(
        string studioName,
        string slug,
        string ownerName,
        string email,
        string password,
        CancellationToken ct
    )
    {
        var passwordCheck = PasswordPolicy.Validate(password);
        if (passwordCheck.IsFailure)
        {
            return Result.Failure<AuthTokens>(passwordCheck.Error!.Value);
        }

        var tenantResult = Tenant.Create(studioName, slug, clock.UtcNow);
        if (tenantResult.IsFailure)
        {
            return Result.Failure<AuthTokens>(tenantResult.Error!.Value);
        }

        var tenant = tenantResult.Value;
        // RLS WITH CHECK exige GUC = tenant_id antes do primeiro SaveChanges.
        if (tenantAccessor is MutableTenantContext mutable)
        {
            mutable.Set(tenant.Id, tenant.Slug.Value, tenant.Status);
        }

        db.Tenants.Add(tenant);

        var serviceTypes = ServiceType.CreateDefaultSeed(tenant.Id);
        foreach (var serviceType in serviceTypes)
        {
            db.ServiceTypes.Add(serviceType);
        }

        foreach (var template in BriefingTemplateSeeder.CreateForTenant(tenant.Id, serviceTypes))
        {
            db.BriefingTemplates.Add(template);
        }

        await EnsureRoleAsync(TenantRoles.Owner, ct);
        await EnsureRoleAsync(TenantRoles.Staff, ct);

        var userId = Guid.NewGuid();
        var user = new AppUser
        {
            Id = userId,
            // UserName soh letras/digitos (validador Identity); unicidade de e-mail e por tenant (RN-TEN-004).
            UserName = userId.ToString("N"),
            Email = email.Trim().ToLowerInvariant(),
            DisplayName = ownerName.Trim(),
            IsOwner = true,
            IsActive = true,
            EmailConfirmed = true,
            TenantId = tenant.Id,
        };

        var create = await users.CreateAsync(user, password);
        if (!create.Succeeded)
        {
            return Result.Failure<AuthTokens>(
                Error.Validation("REGISTRO_FALHOU", string.Join("; ", create.Errors.Select(e => e.Description)))
            );
        }

        await users.AddToRoleAsync(user, TenantRoles.Owner);
        await db.SaveChangesAsync(ct);
        return await IssueAsync(user, TenantRoles.Owner, ct);
    }

    public async Task<Result<AuthTokens>> LoginAsync(string email, string password, CancellationToken ct)
    {
        if (!tenantContext.IsResolved)
        {
            return Result.Failure<AuthTokens>(
                new Error("TENANT_NAO_RESOLVIDO", "Tenant nao foi resolvido no request.")
            );
        }

        var normalized = email.Trim().ToLowerInvariant();
        var user = await users.Users.FirstOrDefaultAsync(
            u => u.TenantId == tenantContext.TenantId && u.Email == normalized,
            ct
        );

        // Sem enumeracao: mesma mensagem para usuario inexistente.
        if (user is null || !user.IsActive)
        {
            return Result.Failure<AuthTokens>(new Error("LOGIN_INVALIDO", "Credenciais invalidas."));
        }

        var result = await signIn.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);
        if (result.IsLockedOut)
        {
            return Result.Failure<AuthTokens>(
                new Error("CONTA_BLOQUEADA", "Conta bloqueada temporariamente apos falhas de login.")
            );
        }

        if (!result.Succeeded)
        {
            return Result.Failure<AuthTokens>(new Error("LOGIN_INVALIDO", "Credenciais invalidas."));
        }

        var role = await ResolveRoleAsync(user);
        return await IssueAsync(user, role, ct);
    }

    public async Task<Result<AuthTokens>> RefreshAsync(string refreshToken, CancellationToken ct)
    {
        if (!tenantContext.IsResolved)
        {
            return Result.Failure<AuthTokens>(
                new Error("TENANT_NAO_RESOLVIDO", "Tenant nao foi resolvido no request.")
            );
        }

        var parts = refreshToken.Split('.', 2);
        if (parts.Length != 2 || !Guid.TryParseExact(parts[0], "N", out var familyId))
        {
            return Result.Failure<AuthTokens>(new Error("REFRESH_INVALIDO", "Refresh token invalido."));
        }

        var family = await db.RefreshTokenFamilies.FirstOrDefaultAsync(
            f => f.Id == familyId && f.TenantId == tenantContext.TenantId,
            ct
        );
        if (family is null)
        {
            return Result.Failure<AuthTokens>(new Error("REFRESH_INVALIDO", "Refresh token invalido."));
        }

        var presentedHash = tokens.HashToken(refreshToken);
        var user = await users.FindByIdAsync(family.UserId.ToString());
        if (user is null || !user.IsActive || user.TenantId != tenantContext.TenantId)
        {
            return Result.Failure<AuthTokens>(new Error("REFRESH_INVALIDO", "Refresh token invalido."));
        }

        var role = await ResolveRoleAsync(user);
        var secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        var newRefresh = $"{family.Id:N}.{secret}";
        var accessBundle = tokens.IssueTokens(user, role);
        var issued = accessBundle with { RefreshToken = newRefresh };

        var rotate = family.Rotate(presentedHash, tokens.HashToken(newRefresh), clock.UtcNow);
        await db.SaveChangesAsync(ct);
        if (rotate.IsFailure)
        {
            return Result.Failure<AuthTokens>(rotate.Error!.Value);
        }

        return issued;
    }

    public async Task<Result<Unit>> RequestPasswordResetAsync(string email, CancellationToken ct)
    {
        // Sem enumeracao: sempre sucesso aparente.
        if (!tenantContext.IsResolved)
        {
            return Unit.Value;
        }

        var normalized = email.Trim().ToLowerInvariant();
        var user = await users.Users.FirstOrDefaultAsync(
            u => u.TenantId == tenantContext.TenantId && u.Email == normalized && u.IsActive,
            ct
        );

        if (user is null)
        {
            return Unit.Value;
        }

        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        db.PasswordResetRequests.Add(
            new PasswordResetRequest
            {
                Id = Guid.NewGuid(),
                TenantId = user.TenantId,
                UserId = user.Id,
                TokenHash = tokens.HashToken(raw),
                ExpiresAt = clock.UtcNow.AddHours(1),
            }
        );
        await db.SaveChangesAsync(ct);

        // Em prod o link vai por e-mail; o token cru nao e logado.
        await notifier.SendEmailAsync(
            user.Email!,
            "Recuperacao de senha — Prata",
            "Use o token de recuperacao fornecido pelo canal seguro do estudio.",
            ct
        );

        // Expomos o token so em desenvolvimento via header interno? Melhor: retornar Unit
        // e testes injetam INotifier spy. Para API de confirmacao o cliente recebe por e-mail.
        PasswordResetDebug.LastRawToken = raw;
        return Unit.Value;
    }

    public async Task<Result<Unit>> ConfirmPasswordResetAsync(
        string token,
        string newPassword,
        CancellationToken ct
    )
    {
        var passwordCheck = PasswordPolicy.Validate(newPassword);
        if (passwordCheck.IsFailure)
        {
            return passwordCheck;
        }

        if (!tenantContext.IsResolved)
        {
            return Error.Validation("TENANT_NAO_RESOLVIDO", "Tenant nao foi resolvido no request.");
        }

        var hash = tokens.HashToken(token);
        var request = await db.PasswordResetRequests.FirstOrDefaultAsync(
            r =>
                r.TenantId == tenantContext.TenantId
                && r.TokenHash == hash
                && r.UsedAt == null
                && r.ExpiresAt > clock.UtcNow,
            ct
        );

        if (request is null)
        {
            return Error.Validation("RESET_INVALIDO", "Token de reset invalido ou expirado.");
        }

        var user = await users.FindByIdAsync(request.UserId.ToString());
        if (user is null)
        {
            return Error.Validation("RESET_INVALIDO", "Token de reset invalido ou expirado.");
        }

        var resetToken = await users.GeneratePasswordResetTokenAsync(user);
        var reset = await users.ResetPasswordAsync(user, resetToken, newPassword);
        if (!reset.Succeeded)
        {
            return Error.Validation("RESET_FALHOU", string.Join("; ", reset.Errors.Select(e => e.Description)));
        }

        request.UsedAt = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }

    public async Task<Result<Guid>> InviteAsync(string email, string role, CancellationToken ct)
    {
        if (!tenantContext.IsResolved)
        {
            return Result.Failure<Guid>(new Error("TENANT_NAO_RESOLVIDO", "Tenant nao foi resolvido."));
        }

        if (role is not (TenantRoles.Owner or TenantRoles.Staff))
        {
            return Result.Failure<Guid>(Error.Validation("CONVITE_PAPEL_INVALIDO", "Papel de convite invalido."));
        }

        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var inviteResult = Invite.Create(
            tenantContext.TenantId!.Value,
            email,
            role,
            tokens.HashToken(raw),
            clock.UtcNow
        );
        if (inviteResult.IsFailure)
        {
            return Result.Failure<Guid>(inviteResult.Error!.Value);
        }

        db.Invites.Add(inviteResult.Value);
        await db.SaveChangesAsync(ct);
        InviteDebug.LastRawToken = raw;
        await notifier.SendEmailAsync(
            email,
            "Convite para o Prata",
            "Voce foi convidado para a equipe do estudio.",
            ct
        );
        return inviteResult.Value.Id;
    }

    public async Task<Result<AuthTokens>> AcceptInviteAsync(
        string inviteToken,
        string name,
        string password,
        CancellationToken ct
    )
    {
        var passwordCheck = PasswordPolicy.Validate(password);
        if (passwordCheck.IsFailure)
        {
            return Result.Failure<AuthTokens>(passwordCheck.Error!.Value);
        }

        if (!tenantContext.IsResolved)
        {
            return Result.Failure<AuthTokens>(
                new Error("TENANT_NAO_RESOLVIDO", "Tenant nao foi resolvido.")
            );
        }

        var hash = tokens.HashToken(inviteToken);
        var invite = await db.Invites.FirstOrDefaultAsync(
            i => i.TenantId == tenantContext.TenantId && i.TokenHash == hash,
            ct
        );
        if (invite is null)
        {
            return Result.Failure<AuthTokens>(Error.Validation("CONVITE_INVALIDO", "Convite invalido."));
        }

        await EnsureRoleAsync(invite.Role, ct);

        var userId = Guid.NewGuid();
        var user = new AppUser
        {
            Id = userId,
            UserName = userId.ToString("N"),
            Email = invite.Email,
            DisplayName = name.Trim(),
            IsOwner = invite.Role == TenantRoles.Owner,
            IsActive = true,
            EmailConfirmed = true,
            TenantId = invite.TenantId,
        };

        var create = await users.CreateAsync(user, password);
        if (!create.Succeeded)
        {
            return Result.Failure<AuthTokens>(
                Error.Validation("CONVITE_ACEITE_FALHOU", string.Join("; ", create.Errors.Select(e => e.Description)))
            );
        }

        await users.AddToRoleAsync(user, invite.Role);
        var accept = invite.Aceitar(user.Id, clock.UtcNow);
        if (accept.IsFailure)
        {
            await users.DeleteAsync(user);
            return Result.Failure<AuthTokens>(accept.Error!.Value);
        }

        await db.SaveChangesAsync(ct);
        return await IssueAsync(user, invite.Role, ct);
    }

    public async Task<Result<Unit>> RemoveTeamMemberAsync(Guid userId, CancellationToken ct)
    {
        if (!tenantContext.IsResolved)
        {
            return Error.Validation("TENANT_NAO_RESOLVIDO", "Tenant nao foi resolvido.");
        }

        var target = await users.Users.FirstOrDefaultAsync(
            u => u.Id == userId && u.TenantId == tenantContext.TenantId,
            ct
        );
        if (target is null)
        {
            return Error.Validation("MEMBRO_NAO_ENCONTRADO", "Membro nao encontrado.");
        }

        var owners = await users.Users.CountAsync(
            u => u.TenantId == tenantContext.TenantId && u.IsOwner && u.IsActive,
            ct
        );
        var gate = TeamPolicy.PodeRemoverOuRebaixarOwner(target.IsOwner, owners);
        if (gate.IsFailure)
        {
            return gate;
        }

        target.IsActive = false;
        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }

    private async Task<AuthTokens> IssueAsync(AppUser user, string role, CancellationToken ct)
    {
        var familyId = Guid.NewGuid();
        var secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        var refresh = $"{familyId:N}.{secret}";
        var accessBundle = tokens.IssueTokens(user, role);
        var issued = accessBundle with { RefreshToken = refresh };

        var family = RefreshTokenFamily.Create(
            familyId,
            user.TenantId,
            user.Id,
            tokens.HashToken(refresh),
            clock.UtcNow,
            TimeSpan.FromDays(jwtOptions.Value.RefreshTokenDays)
        );
        db.RefreshTokenFamilies.Add(family);
        await db.SaveChangesAsync(ct);
        return issued;
    }

    private async Task EnsureRoleAsync(string role, CancellationToken ct)
    {
        if (!await roles.RoleExistsAsync(role))
        {
            await roles.CreateAsync(new AppRole(role) { Id = Guid.NewGuid() });
        }
    }

    private async Task<string> ResolveRoleAsync(AppUser user)
    {
        var userRoles = await users.GetRolesAsync(user);
        if (userRoles.Contains(TenantRoles.Owner))
        {
            return TenantRoles.Owner;
        }

        if (userRoles.Contains(TenantRoles.Staff))
        {
            return TenantRoles.Staff;
        }

        return TenantRoles.Client;
    }
}

/// <summary>Canal de teste para capturar token de reset sem e-mail real.</summary>
public static class PasswordResetDebug
{
    public static string? LastRawToken { get; set; }
}

public static class InviteDebug
{
    public static string? LastRawToken { get; set; }
}

public sealed class NullNotifier : INotifier
{
    public Task SendEmailAsync(
        string to,
        string subject,
        string body,
        CancellationToken cancellationToken = default
    ) => Task.CompletedTask;

    public Task SendTransactionalEmailAsync(
        Guid tenantId,
        string to,
        string type,
        string idempotencyKey,
        string subject,
        string body,
        CancellationToken cancellationToken = default
    ) => Task.CompletedTask;
}
