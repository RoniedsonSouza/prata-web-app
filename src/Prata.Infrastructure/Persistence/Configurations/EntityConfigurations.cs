using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prata.Domain.Catalog;
using Prata.Domain.Common;
using Prata.Domain.Showcase;
using Prata.Domain.Tenancy;
using Prata.Infrastructure.Persistence;

namespace Prata.Infrastructure.Persistence.Configurations;

internal sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("tenant");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Name).HasMaxLength(200).IsRequired();
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(32);
        builder.OwnsOne(
            t => t.Slug,
            slug =>
            {
                slug.Property(s => s.Value).HasColumnName("slug").HasMaxLength(40).IsRequired();
                slug.HasIndex(s => s.Value).IsUnique();
            }
        );
        builder.OwnsOne(
            t => t.Settings,
            settings =>
            {
                settings.Property(s => s.ThemeId).HasColumnName("theme_id").HasConversion<string>();
                settings.Property(s => s.TypePair).HasColumnName("type_pair").HasMaxLength(64);
                settings.Property(s => s.PrimaryColor).HasColumnName("primary_color").HasMaxLength(16);
                settings.Property(s => s.AccentColor).HasColumnName("accent_color").HasMaxLength(16);
                settings.Property(s => s.EffectsEnabled).HasColumnName("effects_enabled");
                settings.Property(s => s.TimeZone).HasColumnName("time_zone").HasMaxLength(64);
                settings.Property(s => s.Currency).HasColumnName("currency").HasConversion<string>();
                settings.Property(s => s.QuoteValidityDays).HasColumnName("quote_validity_days");
                settings.Property(s => s.GalleryExpirationDays).HasColumnName("gallery_expiration_days");
            }
        );
        builder.Ignore(t => t.DomainEvents);
        builder.Ignore(t => t.IsPortfolioPublished);
    }
}

internal sealed class InviteConfiguration : IEntityTypeConfiguration<Invite>
{
    public void Configure(EntityTypeBuilder<Invite> builder)
    {
        builder.ToTable("invite");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.TenantId).IsRequired();
        builder.Property(i => i.Email).HasMaxLength(320).IsRequired();
        builder.Property(i => i.Role).HasMaxLength(64).IsRequired();
        builder.Property(i => i.TokenHash).HasMaxLength(128).IsRequired();
        builder.Property(i => i.CreatedAt).IsRequired();
        builder.Property(i => i.ExpiresAt).IsRequired();
        builder.Property(i => i.Status).HasConversion<string>().HasMaxLength(32);
        builder.HasIndex(i => new { i.TenantId, i.Email });
        builder.Ignore(i => i.DomainEvents);
    }
}

internal sealed class ServiceTypeConfiguration : IEntityTypeConfiguration<ServiceType>
{
    public void Configure(EntityTypeBuilder<ServiceType> builder)
    {
        builder.ToTable("service_type");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.TenantId).IsRequired();
        builder.Property(s => s.Code).HasMaxLength(64).IsRequired();
        builder.Property(s => s.Name).HasMaxLength(200).IsRequired();
        builder.HasIndex(s => new { s.TenantId, s.Code }).IsUnique();
        builder.Ignore(s => s.DomainEvents);
    }
}

internal sealed class PackageConfiguration : IEntityTypeConfiguration<Package>
{
    public void Configure(EntityTypeBuilder<Package> builder)
    {
        builder.ToTable("package");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.TenantId).IsRequired();
        builder.Property(p => p.Name).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Description).HasMaxLength(2000);
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(32);
        builder
            .Property(p => p.Price)
            .HasConversion(
                money => money.HasValue ? money.Value.Amount : (decimal?)null,
                amount => amount.HasValue ? Money.Brl(amount.Value) : null
            )
            .HasColumnName("price_amount")
            .HasColumnType("numeric(14,2)");
        builder.Ignore(p => p.DomainEvents);
    }
}

internal sealed class AddonConfiguration : IEntityTypeConfiguration<Addon>
{
    public void Configure(EntityTypeBuilder<Addon> builder)
    {
        builder.ToTable("addon");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.TenantId).IsRequired();
        builder.Property(a => a.Name).HasMaxLength(200).IsRequired();
        builder
            .Property(a => a.Price)
            .HasConversion(money => money.Amount, amount => Money.Brl(amount))
            .HasColumnName("price_amount")
            .HasColumnType("numeric(14,2)");
        builder.Ignore(a => a.DomainEvents);
    }
}

internal sealed class CollectionConfiguration : IEntityTypeConfiguration<Collection>
{
    public void Configure(EntityTypeBuilder<Collection> builder)
    {
        builder.ToTable("collection");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.TenantId).IsRequired();
        builder.Property(c => c.Slug).HasMaxLength(80).IsRequired();
        builder.Property(c => c.Title).HasMaxLength(200).IsRequired();
        builder.Property(c => c.Status).HasConversion<string>().HasMaxLength(32);
        builder.HasIndex(c => new { c.TenantId, c.Slug }).IsUnique();
        builder.Ignore(c => c.DomainEvents);
        builder.Ignore(c => c.Items);
    }
}

internal sealed class CollectionItemConfiguration : IEntityTypeConfiguration<CollectionItem>
{
    public void Configure(EntityTypeBuilder<CollectionItem> builder)
    {
        builder.ToTable("collection_item");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.TenantId).IsRequired();
        builder.Property(i => i.ObjectKey).HasMaxLength(512).IsRequired();
        builder.Property(i => i.AltText).HasMaxLength(500).IsRequired();
        builder.Property(i => i.Consent).HasConversion<string>().HasMaxLength(32);
    }
}

internal sealed class PageContentConfiguration : IEntityTypeConfiguration<PageContent>
{
    public void Configure(EntityTypeBuilder<PageContent> builder)
    {
        builder.ToTable("page_content");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.TenantId).IsRequired();
        builder.Property(p => p.Path).HasMaxLength(200).IsRequired();
        builder.Property(p => p.BodyMarkdown).IsRequired();
        builder.OwnsOne(
            p => p.Seo,
            seo =>
            {
                seo.Property(s => s.Title).HasColumnName("seo_title").HasMaxLength(200);
                seo.Property(s => s.Description).HasColumnName("seo_description").HasMaxLength(500);
                seo.Property(s => s.OgImageUrl).HasColumnName("seo_og_image_url").HasMaxLength(512);
            }
        );
        builder.Ignore(p => p.DomainEvents);
    }
}

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_message");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Type).HasMaxLength(200).IsRequired();
        builder.Property(o => o.Payload).IsRequired();
        builder.HasIndex(o => o.ProcessedAt);
    }
}

internal sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_log");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Action).HasMaxLength(128).IsRequired();
        builder.Property(a => a.EntityType).HasMaxLength(128).IsRequired();
    }
}

internal sealed class PlatformUserConfiguration : IEntityTypeConfiguration<PlatformUser>
{
    public void Configure(EntityTypeBuilder<PlatformUser> builder)
    {
        builder.ToTable("platform_user");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Email).HasMaxLength(320).IsRequired();
        builder.HasIndex(u => u.Email).IsUnique();
        builder.Property(u => u.PasswordHash).IsRequired();
        builder.Property(u => u.Role).HasMaxLength(64).IsRequired();
    }
}
