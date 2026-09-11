using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Prata.Application.Abstractions;
using Prata.Infrastructure.Identity;
using Prata.Infrastructure.Persistence;

namespace Prata.Api.IntegrationTests;

/// <summary>
/// API real + Postgres Testcontainers (papel prata_app + RLS).
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgresFixture _fixture;
    private string _appCs = null!;
    private string _ownerCs = null!;

    public ApiFactory(PostgresFixture fixture) => _fixture = fixture;

    public async ValueTask InitializeAsync()
    {
        _ownerCs = _fixture.OwnerConnectionString;
        await _fixture.EnsureAppRoleAsync();

        await using (var owner = CreateOwnerDb())
        {
            await owner.Database.EnsureDeletedAsync();
            await owner.Database.MigrateAsync();
        }

        _appCs = _fixture.AppConnectionString;
        await _fixture.GrantAppRoleAsync();
    }

    public new async ValueTask DisposeAsync() => await base.DisposeAsync();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        if (string.IsNullOrWhiteSpace(_appCs) || string.IsNullOrWhiteSpace(_ownerCs))
        {
            throw new InvalidOperationException(
                "ApiFactory.InitializeAsync deve rodar antes de CreateClient."
            );
        }

        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration(
            (_, config) =>
            {
                config.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:PrataOwner"] = _ownerCs,
                        ["ConnectionStrings:PrataApp"] = _appCs,
                        ["ConnectionStrings:Prata"] = _appCs,
                        ["ConnectionStrings:Default"] = _appCs,
                        ["Auth:Jwt:Issuer"] = "test",
                        ["Auth:Jwt:Audience"] = "test",
                        ["Auth:Jwt:SigningKey"] = "TEST_SIGNING_KEY_32_CHARS_MINIMUM!!",
                        ["Auth:Jwt:AccessTokenMinutes"] = "15",
                        ["Auth:Jwt:RefreshTokenDays"] = "30",
                        ["Platform:BootstrapSecret"] = "test-bootstrap-secret",
                        ["Revalidate:Secret"] = "test-revalidate",
                    }
                );
            }
        );
        builder.ConfigureTestServices(services =>
        {
            // Garante DbContext no Postgres do Testcontainers (nao no appsettings localhost).
            var descriptors = services
                .Where(d =>
                    d.ServiceType == typeof(DbContextOptions<PrataDbContext>)
                    || d.ServiceType == typeof(PrataDbContext)
                )
                .ToList();
            foreach (var d in descriptors)
            {
                services.Remove(d);
            }

            services.AddScoped<TenantRlsConnectionInterceptor>();
            services.AddDbContext<PrataDbContext>(
                (sp, options) =>
                    options
                        .UseNpgsql(_appCs)
                        .UseSnakeCaseNamingConvention()
                        .AddInterceptors(sp.GetRequiredService<TenantRlsConnectionInterceptor>())
            );

            services.PostConfigure<Microsoft.AspNetCore.Authentication.AuthenticationOptions>(
                options =>
                {
                    options.DefaultAuthenticateScheme = Microsoft
                        .AspNetCore
                        .Authentication
                        .JwtBearer
                        .JwtBearerDefaults
                        .AuthenticationScheme;
                    options.DefaultChallengeScheme = Microsoft
                        .AspNetCore
                        .Authentication
                        .JwtBearer
                        .JwtBearerDefaults
                        .AuthenticationScheme;
                }
            );
            services.PostConfigure<Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerOptions>(
                Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme,
                options =>
                {
                    options.MapInboundClaims = false;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateIssuerSigningKey = true,
                        ValidateLifetime = true,
                        ValidIssuer = "test",
                        ValidAudience = "test",
                        IssuerSigningKey = new SymmetricSecurityKey(
                            Encoding.UTF8.GetBytes("TEST_SIGNING_KEY_32_CHARS_MINIMUM!!")
                        ),
                        RoleClaimType = "role",
                        NameClaimType = JwtRegisteredClaimNames.Sub,
                    };
                }
            );
            services.PostConfigure<JwtOptions>(options =>
            {
                options.Issuer = "test";
                options.Audience = "test";
                options.SigningKey = "TEST_SIGNING_KEY_32_CHARS_MINIMUM!!";
            });
        });
    }

    public HttpClient CreateTenantClient(string slug)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Slug", slug);
        return client;
    }

    private PrataDbContext CreateOwnerDb()
    {
        var options = new DbContextOptionsBuilder<PrataDbContext>()
            .UseNpgsql(_ownerCs)
            .UseSnakeCaseNamingConvention()
            .Options;
        return new PrataDbContext(options, new MutableTenantContext());
    }
}

[Collection(nameof(PostgresCollection))]
public sealed class AuthHttpTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly PostgresFixture _fixture;
    private ApiFactory _factory = null!;

    public AuthHttpTests(PostgresFixture fixture) => _fixture = fixture;

    public async ValueTask InitializeAsync()
    {
        _factory = new ApiFactory(_fixture);
        await _factory.InitializeAsync();
    }

    public async ValueTask DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task RN_TEN_010_lockout_apos_cinco_falhas()
    {
        var slug = UniqueSlug("lock");
        using var client = _factory.CreateTenantClient(slug);

        await RegisterAsync(client, slug, "owner@lock.com", "senha12345");

        for (var i = 0; i < 5; i++)
        {
            var fail = await client.PostAsJsonAsync(
                "/v1/auth/login",
                new { email = "owner@lock.com", password = "senha-errada!" }
            );
            // A tentativa que atinge o limite ja pode responder CONTA_BLOQUEADA.
            fail.StatusCode.Should()
                .BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Conflict);
        }

        var locked = await client.PostAsJsonAsync(
            "/v1/auth/login",
            new { email = "owner@lock.com", password = "senha12345" }
        );
        locked.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await locked.Content.ReadAsStringAsync()).Should().Contain("CONTA_BLOQUEADA");
    }

    [Fact]
    public async Task Refresh_reuso_invalida_familia()
    {
        var slug = UniqueSlug("ref");
        using var client = _factory.CreateTenantClient(slug);
        var tokens = await RegisterAsync(client, slug, "owner@ref.com", "senha12345");

        var first = await client.PostAsJsonAsync(
            "/v1/auth/refresh",
            new { refreshToken = tokens.RefreshToken }
        );
        first.IsSuccessStatusCode.Should().BeTrue(await first.Content.ReadAsStringAsync());

        var reuse = await client.PostAsJsonAsync(
            "/v1/auth/refresh",
            new { refreshToken = tokens.RefreshToken }
        );
        reuse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await reuse.Content.ReadAsStringAsync()).Should().Contain("REFRESH_REUSADO");
    }

    [Fact]
    public async Task Docs12_staff_nao_acessa_financeiro()
    {
        var slug = UniqueSlug("fin");
        using var client = _factory.CreateTenantClient(slug);
        var owner = await RegisterAsync(client, slug, "owner@fin.com", "senha12345");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            owner.AccessToken
        );
        var invite = await client.PostAsJsonAsync(
            "/v1/studio/team/invites",
            new { email = "staff@fin.com", role = "tenant.staff" }
        );
        invite.IsSuccessStatusCode.Should().BeTrue(await invite.Content.ReadAsStringAsync());

        var raw = InviteDebug.LastRawToken;
        raw.Should().NotBeNullOrEmpty();

        var accepted = await client.PostAsJsonAsync(
            "/v1/auth/invites/accept",
            new
            {
                token = raw,
                name = "Staff",
                password = "senha12345",
            }
        );
        accepted.IsSuccessStatusCode.Should().BeTrue(await accepted.Content.ReadAsStringAsync());
        var staff = await accepted.Content.ReadFromJsonAsync<TokenResponse>(JsonOptions);

        using var staffClient = _factory.CreateTenantClient(slug);
        staffClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            staff!.AccessToken
        );

        var response = await staffClient.GetAsync("/v1/studio/finance");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await response.Content.ReadAsStringAsync()).Should().Contain("SEM_PERMISSAO");
    }

    [Fact]
    public async Task Docs12_platform_admin_nao_le_briefing()
    {
        using var client = _factory.CreateClient();
        var bootstrap = await client.PostAsJsonAsync(
            "/v1/platform/auth/bootstrap",
            new
            {
                bootstrapSecret = "test-bootstrap-secret",
                email = "admin@prata.app",
                password = "senha12345",
            }
        );
        bootstrap.IsSuccessStatusCode.Should().BeTrue(await bootstrap.Content.ReadAsStringAsync());

        var login = await client.PostAsJsonAsync(
            "/v1/platform/auth/login",
            new { email = "admin@prata.app", password = "senha12345" }
        );
        login.IsSuccessStatusCode.Should().BeTrue(await login.Content.ReadAsStringAsync());
        var tokens = await login.Content.ReadFromJsonAsync<TokenResponse>(JsonOptions);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            tokens!.AccessToken
        );

        var briefing = await client.GetAsync($"/v1/platform/briefings/{Guid.NewGuid()}");
        briefing.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await briefing.Content.ReadAsStringAsync()).Should().Contain("SEM_PERMISSAO");
    }

    [Fact]
    public async Task Docs12_convidado_nao_baixa_original()
    {
        var slug = UniqueSlug("gal");
        using var client = _factory.CreateTenantClient(slug);
        var owner = await RegisterAsync(client, slug, "owner@gal.com", "senha12345");

        var anon = await client.GetAsync(
            $"/v1/galleries/{Guid.NewGuid()}/photos/{Guid.NewGuid()}/original"
        );
        anon.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var tenantId = ReadClaim(owner.AccessToken, "tenant_id");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            IssueGuestJwt(tenantId)
        );
        var guest = await client.GetAsync(
            $"/v1/galleries/{Guid.NewGuid()}/photos/{Guid.NewGuid()}/original"
        );
        guest.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await guest.Content.ReadAsStringAsync()).Should().Contain("SEM_PERMISSAO");
    }

    private static async Task<TokenResponse> RegisterAsync(
        HttpClient client,
        string slug,
        string email,
        string password
    )
    {
        var response = await client.PostAsJsonAsync(
            "/v1/auth/studio/register",
            new
            {
                studioName = $"Estudio {slug}",
                slug,
                ownerName = "Dona",
                email,
                password,
            }
        );
        response.IsSuccessStatusCode.Should().BeTrue(await response.Content.ReadAsStringAsync());
        var tokens = await response.Content.ReadFromJsonAsync<TokenResponse>(JsonOptions);
        tokens.Should().NotBeNull();
        return tokens!;
    }

    private static string UniqueSlug(string prefix) =>
        $"{prefix}-{Guid.NewGuid():N}"[.. Math.Min(20, prefix.Length + 13)];

    private static string ReadClaim(string jwt, string type)
    {
        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(jwt);
        return token.Claims.First(c => c.Type == type).Value;
    }

    private static string IssueGuestJwt(string tenantId)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes("TEST_SIGNING_KEY_32_CHARS_MINIMUM!!")
        );
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: "test",
            audience: "test",
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
                new Claim("role", "convidado"),
                new Claim(ClaimTypes.Role, "convidado"),
                new Claim("tenant_id", tenantId),
            ],
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: creds
        );
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public sealed record TokenResponse(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt);
