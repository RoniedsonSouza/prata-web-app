using AwesomeAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Prata.Application.Abstractions;
using Prata.Domain.Tenancy;
using Prata.Infrastructure.Identity;
using Prata.Infrastructure.Persistence;

namespace Prata.Api.IntegrationTests.Tenancy;

[Collection(nameof(PostgresCollection))]
public sealed class AuthFlowTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;
    private ServiceProvider _provider = null!;

    public AuthFlowTests(PostgresFixture fixture) => _fixture = fixture;

    public async ValueTask InitializeAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddScoped<MutableTenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<MutableTenantContext>());
        services.AddScoped<ITenantContextAccessor>(sp => sp.GetRequiredService<MutableTenantContext>());
        services.AddSingleton<INotifier, NullNotifier>();
        services.Configure<JwtOptions>(o =>
        {
            o.Issuer = "test";
            o.Audience = "test";
            o.SigningKey = "TEST_SIGNING_KEY_32_CHARS_MINIMUM!!";
            o.AccessTokenMinutes = 15;
            o.RefreshTokenDays = 30;
        });
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddDbContext<PrataDbContext>(options =>
            options.UseNpgsql(_fixture.OwnerConnectionString).UseSnakeCaseNamingConvention()
        );
        services
            .AddIdentity<AppUser, AppRole>(options =>
            {
                options.Password.RequiredLength = PasswordPolicy.MinLength;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Lockout.MaxFailedAccessAttempts = PasswordPolicy.MaxFailedAttempts;
                options.Lockout.DefaultLockoutTimeSpan = PasswordPolicy.LockoutDuration;
                options.User.RequireUniqueEmail = false;
            })
            .AddEntityFrameworkStores<PrataDbContext>()
            .AddDefaultTokenProviders();
        services.AddScoped<IAuthService, AuthService>();

        _provider = services.BuildServiceProvider();

        await using var scope = _provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PrataDbContext>();
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
    }

    [Fact]
    public async Task RN_TEN_004_mesmo_email_em_tenants_diferentes_ok()
    {
        await using var scope = _provider.CreateAsyncScope();
        var auth = scope.ServiceProvider.GetRequiredService<IAuthService>();

        var a = await auth.RegisterStudioAsync(
            "Estudio A",
            "estudio-a",
            "Ana",
            "dona@email.com",
            "senha12345",
            TestContext.Current.CancellationToken
        );
        var b = await auth.RegisterStudioAsync(
            "Estudio B",
            "estudio-b",
            "Bia",
            "dona@email.com",
            "senha12345",
            TestContext.Current.CancellationToken
        );

        a.IsSuccess.Should().BeTrue("registro A: {0}", a.Error);
        b.IsSuccess.Should().BeTrue("registro B: {0}", b.Error);
    }

    [Fact]
    public async Task RN_TEN_007_nao_remove_ultimo_owner()
    {
        await using var scope = _provider.CreateAsyncScope();
        var auth = scope.ServiceProvider.GetRequiredService<IAuthService>();
        var tenantCtx = scope.ServiceProvider.GetRequiredService<MutableTenantContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        var reg = await auth.RegisterStudioAsync(
            "Estudio C",
            "estudio-c",
            "Ana",
            "owner@c.com",
            "senha12345",
            TestContext.Current.CancellationToken
        );
        reg.IsSuccess.Should().BeTrue("registro: {0}", reg.Error);

        // Sem tenant no contexto o filtro global esconde ITenantOwned — leitura explicita no teste.
        var owner = await users
            .Users.IgnoreQueryFilters()
            .SingleAsync(u => u.Email == "owner@c.com", TestContext.Current.CancellationToken);
        tenantCtx.Set(owner.TenantId, "estudio-c", TenantStatus.Rascunho);

        var remove = await auth.RemoveTeamMemberAsync(owner.Id, TestContext.Current.CancellationToken);
        remove.IsFailure.Should().BeTrue();
        remove.Error!.Value.Code.Should().Be("ULTIMO_OWNER");
    }
}
