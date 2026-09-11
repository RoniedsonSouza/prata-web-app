using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Prata.Application.Abstractions;
using Prata.Domain.Billing;
using Prata.Domain.Briefing;
using Prata.Domain.Catalog;
using Prata.Domain.Common;
using Prata.Domain.Notifications;
using Prata.Domain.Sales;
using Prata.Domain.Showcase;
using Prata.Domain.Tenancy;
using Prata.Infrastructure.Identity;

namespace Prata.Infrastructure.Persistence;

public sealed class PrataDbContext : IdentityDbContext<AppUser, AppRole, Guid>
{
    private readonly ITenantContext _tenantContext;

    public PrataDbContext(DbContextOptions<PrataDbContext> options, ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<Invite> Invites => Set<Invite>();

    public DbSet<RefreshTokenFamily> RefreshTokenFamilies => Set<RefreshTokenFamily>();

    public DbSet<ServiceType> ServiceTypes => Set<ServiceType>();

    public DbSet<Package> Packages => Set<Package>();

    public DbSet<Addon> Addons => Set<Addon>();

    public DbSet<Collection> Collections => Set<Collection>();

    public DbSet<CollectionItem> CollectionItems => Set<CollectionItem>();

    public DbSet<PageContent> PageContents => Set<PageContent>();

    public DbSet<Client> Clients => Set<Client>();

    public DbSet<Order> Orders => Set<Order>();

    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    public DbSet<Quote> Quotes => Set<Quote>();

    public DbSet<BriefingTemplate> BriefingTemplates => Set<BriefingTemplate>();

    public DbSet<Question> BriefingQuestions => Set<Question>();

    public DbSet<Answer> BriefingAnswers => Set<Answer>();

    public DbSet<BriefingConsent> BriefingConsents => Set<BriefingConsent>();

    public DbSet<NotificationMessage> NotificationMessages => Set<NotificationMessage>();

    public DbSet<Payment> Payments => Set<Payment>();

    public DbSet<Installment> Installments => Set<Installment>();

    public DbSet<SplitRule> SplitRules => Set<SplitRule>();

    public DbSet<PayoutAccount> PayoutAccounts => Set<PayoutAccount>();

    public DbSet<Payout> Payouts => Set<Payout>();

    public DbSet<PaymentEvent> PaymentEvents => Set<PaymentEvent>();

    public DbSet<ReconciliationIssue> ReconciliationIssues => Set<ReconciliationIssue>();

    public DbSet<Booking> Bookings => Set<Booking>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<PlatformUser> PlatformUsers => Set<PlatformUser>();

    public DbSet<PasswordResetRequest> PasswordResetRequests => Set<PasswordResetRequest>();

    /// <summary>
    /// Valor lido a cada query pelo filtro global (captura via this no HasQueryFilter).
    /// </summary>
    private Guid TenantIdForFilter => _tenantContext.TenantId ?? Guid.Empty;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(PrataDbContext).Assembly);
        ApplyTenantQueryFilters(builder);
    }

    private void ApplyTenantQueryFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ITenantOwned).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            var method = typeof(PrataDbContext)
                .GetMethod(
                    nameof(SetTenantFilter),
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
                )!
                .MakeGenericMethod(entityType.ClrType);
            method.Invoke(this, [modelBuilder]);
        }
    }

    private void SetTenantFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ITenantOwned
    {
        modelBuilder.Entity<TEntity>().HasQueryFilter(e => e.TenantId == TenantIdForFilter);
    }
}

/// <summary>
/// Outbox na mesma transacao do agregado (docs/02 §5.3).
/// </summary>
public sealed class OutboxMessage
{
    public Guid Id { get; set; }

    public Guid? TenantId { get; set; }

    public string Type { get; set; } = string.Empty;

    public string Payload { get; set; } = string.Empty;

    public DateTimeOffset OccurredAt { get; set; }

    public DateTimeOffset? ProcessedAt { get; set; }
}

public sealed class AuditLog
{
    public Guid Id { get; set; }

    public Guid? TenantId { get; set; }

    public Guid? UserId { get; set; }

    public string Action { get; set; } = string.Empty;

    public string EntityType { get; set; } = string.Empty;

    public Guid? EntityId { get; set; }

    public DateTimeOffset At { get; set; }

    public string? Metadata { get; set; }
}

/// <summary>
/// Usuario da plataforma — fora do modelo de tenant (E1 §3.8).
/// </summary>
public sealed class PlatformUser
{
    public Guid Id { get; set; }

    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string Role { get; set; } = "platform.admin";

    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Pedido de reset de senha escopado ao tenant. Sem enumeracao de e-mail.
/// </summary>
public sealed class PasswordResetRequest : ITenantOwned
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid UserId { get; set; }

    public string TokenHash { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? UsedAt { get; set; }
}
