using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Prata.Application.Abstractions;
using Prata.Application.Billing;
using Prata.Application.Briefing;
using Prata.Application.Sales;
using Prata.Domain.Tenancy;
using Prata.Infrastructure.Billing;
using Prata.Infrastructure.Briefing;
using Prata.Infrastructure.Contracts;
using Prata.Infrastructure.Identity;
using Prata.Infrastructure.Imaging;
using Prata.Infrastructure.Jobs;
using Prata.Infrastructure.Notifications;
using Prata.Infrastructure.Payments;
using Prata.Infrastructure.Persistence;
using Prata.Infrastructure.Storage;

namespace Prata.Infrastructure;

public static class DependencyInjection
{
    public const string OwnerConnectionName = "PrataOwner";
    public const string AppConnectionName = "PrataApp";

    public static IServiceCollection AddPrataInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddScoped<MutableTenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<MutableTenantContext>());
        services.AddScoped<ITenantContextAccessor>(sp => sp.GetRequiredService<MutableTenantContext>());
        services.AddScoped<INotifier, SmtpNotifier>();
        services.AddSingleton<IPaymentGateway, FakePaymentGateway>();
        services.AddScoped<IDepositPaymentService, DepositPaymentService>();
        services.AddScoped<IOrderConfirmationService, OrderConfirmationService>();
        services.AddScoped<IWebhookPaymentProcessor, WebhookPaymentProcessor>();
        services.AddScoped<IReconcilePaymentsProcessor, ReconcilePaymentsProcessor>();
        services.AddScoped<IContractService, ContractService>();
        services.AddScoped<ISchedulingService, SchedulingService>();
        services.AddScoped<ISignatureProvider, OwnSignatureProvider>();
        services.AddSingleton<IPdfRenderer, SimpleContractPdfRenderer>();
        services.AddScoped<IWhatsAppCloudSender, OptInWhatsAppCloudSender>();
        services.AddScoped<IReminderProcessor, ReminderProcessor>();
        services.AddSingleton<ISignedFichaService, SignedFichaService>();
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IOutboxProcessor, OutboxProcessor>();
        services.AddScoped<PortfolioDerivativeProcessor>();
        services.AddScoped<GalleryOutboxProcessor>();
        services.AddPrataStorage(configuration);
        services.AddHttpClient("revalidate");
        services.AddOpenTelemetryPrata(configuration);

        var appConnection =
            configuration.GetConnectionString(AppConnectionName)
            ?? configuration.GetConnectionString("Prata")
            ?? configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string PrataApp (ou Default) e obrigatoria.");

        EnsureNotOwnerRole(appConnection, configuration);

        services.AddScoped<TenantRlsConnectionInterceptor>();
        services.AddDbContext<PrataDbContext>(
            (sp, options) =>
                options
                    .UseNpgsql(appConnection)
                    .UseSnakeCaseNamingConvention()
                    .AddInterceptors(sp.GetRequiredService<TenantRlsConnectionInterceptor>())
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
                options.Lockout.AllowedForNewUsers = true;
                options.User.RequireUniqueEmail = false; // unicidade e (tenant_id, email)
            })
            .AddEntityFrameworkStores<PrataDbContext>()
            .AddDefaultTokenProviders();

        var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(
                            string.IsNullOrWhiteSpace(jwt.SigningKey) ? "DEV_ONLY_CHANGE_ME_32CHARS_MINIMUM!!" : jwt.SigningKey
                        )
                    ),
                    ClockSkew = TimeSpan.FromMinutes(1),
                    RoleClaimType = "role",
                    NameClaimType = JwtRegisteredClaimNames.Sub,
                };
            });
        // AddIdentity redefine o default para cookies — forca JWT para a API.
        services.PostConfigure<Microsoft.AspNetCore.Authentication.AuthenticationOptions>(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        });
        services.AddAuthorization(options =>
        {
            options.AddPolicy(
                "PlatformAdmin",
                policy => policy.RequireAssertion(ctx => ctx.User.IsInRole("platform.admin") || ctx.User.HasClaim("role", "platform.admin"))
            );
            options.AddPolicy(
                "TenantOwner",
                policy =>
                    policy.RequireAssertion(ctx => ctx.User.IsInRole(TenantRoles.Owner) || ctx.User.HasClaim("role", TenantRoles.Owner))
            );
        });

        services.AddScoped<IClientRepository, ClientRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<ICatalogReader, CatalogReader>();
        services.AddScoped<ITenantReader, TenantReader>();
        services.AddScoped<IBriefingTemplateRepository, BriefingTemplateRepository>();
        services.AddScoped<IAnswerRepository, AnswerRepository>();
        services.AddScoped<IBriefingConsentRepository, BriefingConsentRepository>();
        services.AddScoped<IExpireQuotesProcessor, ExpireQuotesProcessor>();
        services.AddScoped<IExpireGalleriesProcessor, ExpireGalleriesProcessor>();
        services.AddScoped<IPurgeSensitiveBriefingProcessor, PurgeSensitiveBriefingProcessor>();
        services.AddScoped<IDirectionSheetRenderer, DirectionSheetRenderer>();
        services.AddScoped<IWhatsAppLinkGenerator, WhatsAppLinkGenerator>();
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<PrataDbContextUnitOfWork>());
        services.AddScoped<PrataDbContextUnitOfWork>();

        return services;
    }

    private static IServiceCollection AddOpenTelemetryPrata(this IServiceCollection services, IConfiguration configuration)
    {
        var otlp = configuration["OpenTelemetry:OtlpEndpoint"];
        services
            .AddOpenTelemetry()
            .ConfigureResource(r => r.AddService("Prata.Api"))
            .WithTracing(tracing =>
            {
                tracing.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation().AddNpgsql();
                if (!string.IsNullOrWhiteSpace(otlp))
                {
                    tracing.AddOtlpExporter(o => o.Endpoint = new Uri(otlp));
                }
            });
        return services;
    }

    /// <summary>
    /// RN-TEN-012 — a API se recusa a subir conectada como owner.
    /// </summary>
    public static void EnsureNotOwnerRole(string appConnection, IConfiguration configuration)
    {
        var ownerConnection = configuration.GetConnectionString(OwnerConnectionName) ?? configuration.GetConnectionString("PrataMigration");
        if (
            !string.IsNullOrWhiteSpace(ownerConnection) && string.Equals(appConnection, ownerConnection, StringComparison.OrdinalIgnoreCase)
        )
        {
            throw new InvalidOperationException("API recusou subir: connection string de aplicacao nao pode ser a do owner (RN-TEN-012).");
        }

        if (
            appConnection.Contains("Username=prata_owner", StringComparison.OrdinalIgnoreCase)
            || appConnection.Contains("User ID=prata_owner", StringComparison.OrdinalIgnoreCase)
            || appConnection.Contains("User Id=prata_owner", StringComparison.OrdinalIgnoreCase)
        )
        {
            throw new InvalidOperationException("API recusou subir: conexao como prata_owner e proibida em runtime (RN-TEN-012).");
        }
    }
}

internal sealed class PrataDbContextUnitOfWork(PrataDbContext dbContext) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => dbContext.SaveChangesAsync(cancellationToken);
}

public interface IOutboxProcessor
{
    Task<int> ProcessPendingAsync(CancellationToken cancellationToken = default);
}

public sealed class OutboxProcessor(
    PrataDbContext db,
    IDateTimeProvider clock,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    PortfolioDerivativeProcessor derivatives,
    GalleryOutboxProcessor galleryOutbox
) : IOutboxProcessor
{
    public async Task<int> ProcessPendingAsync(CancellationToken cancellationToken = default)
    {
        var derivativeCount = await derivatives.ProcessPendingAsync(cancellationToken);
        var galleryCount = await galleryOutbox.ProcessPendingAsync(cancellationToken);

        var pending = await db
            .OutboxMessages.Where(m =>
                m.ProcessedAt == null
                && m.Type != "GerarDerivadasPortfolio"
                && m.Type != "GerarDerivadasGaleria"
                && m.Type != "MontarZipGaleria"
            )
            .OrderBy(m => m.OccurredAt)
            .Take(50)
            .ToListAsync(cancellationToken);

        foreach (var message in pending)
        {
            if (message.Type == "ColecaoPublicada")
            {
                await TryRevalidateAsync(message.Payload, cancellationToken);
            }

            message.ProcessedAt = clock.UtcNow;
        }

        if (pending.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return pending.Count + derivativeCount + galleryCount;
    }

    private async Task TryRevalidateAsync(string payload, CancellationToken cancellationToken)
    {
        var baseUrl = configuration["Revalidate:FrontBaseUrl"];
        var secret = configuration["Revalidate:Secret"];
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(secret) || secret == "CHANGE_ME")
        {
            return;
        }

        using var client = httpClientFactory.CreateClient("revalidate");
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl.TrimEnd('/')}/api/revalidate");
        request.Headers.TryAddWithoutValidation("X-Prata-Revalidate", secret);
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
        try
        {
            await client.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException)
        {
            // Worker nao deve travar a fila se o front estiver offline.
        }
    }
}
