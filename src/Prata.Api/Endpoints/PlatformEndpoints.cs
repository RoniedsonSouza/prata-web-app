using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Prata.Application.Abstractions;
using Prata.Infrastructure.Identity;
using Prata.Infrastructure.Persistence;

namespace Prata.Api.Endpoints;

public static class PlatformEndpoints
{
    public static IEndpointRouteBuilder MapPlatformAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost(
            "/v1/platform/auth/login",
            async (
                PlatformLoginRequest body,
                PrataDbContext db,
                IOptions<JwtOptions> jwtOptions,
                IDateTimeProvider clock,
                CancellationToken ct
            ) =>
            {
                var email = body.Email.Trim().ToLowerInvariant();
                var user = await db.PlatformUsers.FirstOrDefaultAsync(
                    u => u.Email == email && u.IsActive,
                    ct
                );
                if (user is null || !VerifyPassword(body.Password, user.PasswordHash))
                {
                    return Results.Json(
                        new { type = "LOGIN_INVALIDO", title = "Credenciais invalidas." },
                        statusCode: StatusCodes.Status401Unauthorized
                    );
                }

                var opts = jwtOptions.Value;
                var expires = clock.UtcNow.AddMinutes(opts.AccessTokenMinutes);
                var claims = new[]
                {
                    new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                    new Claim(JwtRegisteredClaimNames.Email, user.Email),
                    new Claim(ClaimTypes.Role, user.Role),
                    new Claim("role", user.Role),
                };
                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(opts.SigningKey));
                var jwt = new JwtSecurityToken(
                    opts.Issuer,
                    opts.Audience,
                    claims,
                    expires: expires.UtcDateTime,
                    signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
                );
                return Results.Ok(
                    new
                    {
                        accessToken = new JwtSecurityTokenHandler().WriteToken(jwt),
                        expiresAt = expires,
                    }
                );
            }
        );

        app.MapPost(
            "/v1/platform/auth/bootstrap",
            async (
                PlatformBootstrapRequest body,
                PrataDbContext db,
                IConfiguration config,
                CancellationToken ct
            ) =>
            {
                var expected = config["Platform:BootstrapSecret"] ?? "CHANGE_ME";
                if (body.BootstrapSecret != expected || expected == "CHANGE_ME")
                {
                    return Results.Unauthorized();
                }

                if (await db.PlatformUsers.AnyAsync(ct))
                {
                    return Results.Conflict(new { type = "JA_BOOTSTRAPADO" });
                }

                db.PlatformUsers.Add(
                    new PlatformUser
                    {
                        Id = Guid.NewGuid(),
                        Email = body.Email.Trim().ToLowerInvariant(),
                        PasswordHash = HashPassword(body.Password),
                        Role = "platform.admin",
                        IsActive = true,
                    }
                );
                await db.SaveChangesAsync(ct);
                return Results.Created("/v1/platform/auth/login", new { ok = true });
            }
        );

        return app;
    }

    private static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            100_000,
            HashAlgorithmName.SHA256,
            32
        );
        return $"pbkdf2${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    private static bool VerifyPassword(string password, string stored)
    {
        var parts = stored.Split('$', 3);
        if (parts.Length != 3 || parts[0] != "pbkdf2")
        {
            return false;
        }

        var salt = Convert.FromBase64String(parts[1]);
        var expected = Convert.FromBase64String(parts[2]);
        var actual = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            100_000,
            HashAlgorithmName.SHA256,
            32
        );
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}

public sealed record PlatformLoginRequest(string Email, string Password);

public sealed record PlatformBootstrapRequest(string BootstrapSecret, string Email, string Password);
