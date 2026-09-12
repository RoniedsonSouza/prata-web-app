using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prata.Domain.Delivery;

namespace Prata.Infrastructure.Persistence.Configurations;

internal sealed class GalleryConfiguration : IEntityTypeConfiguration<Gallery>
{
    public void Configure(EntityTypeBuilder<Gallery> builder)
    {
        builder.ToTable("gallery");
        builder.HasKey(g => g.Id);
        builder.Property(g => g.TenantId).IsRequired();
        builder.Property(g => g.OrderId).IsRequired();
        builder.Property(g => g.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(g => g.PhotoLimit).IsRequired();
        builder.Property(g => g.PortfolioConsent).HasConversion<string>().HasMaxLength(32);
        builder.Property(g => g.HasMinor).IsRequired();
        builder.Property(g => g.ExpiresAt).IsRequired();
        builder.Property(g => g.CreatedAt).IsRequired();

        builder
            .HasMany<Photo>("_photos")
            .WithOne()
            .HasForeignKey(p => p.GalleryId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasMany<Selection>("_selections")
            .WithOne()
            .HasForeignKey(s => s.GalleryId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation("_photos").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation("_selections").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(g => g.TenantId);
        builder.HasIndex(g => new { g.TenantId, g.OrderId }).IsUnique();
        builder.HasIndex(g => new { g.TenantId, g.ExpiresAt });
        builder.Ignore(g => g.DomainEvents);
        builder.Ignore(g => g.Photos);
        builder.Ignore(g => g.Selections);
        builder.Ignore(g => g.ExtraPhotosCount);
        builder.Ignore(g => g.PermiteVisualizacaoBaixa);
        builder.Ignore(g => g.PermiteAltaResolucao);
        builder.Ignore(g => g.PodePublicarNoPortfolio);
    }
}

internal sealed class PhotoConfiguration : IEntityTypeConfiguration<Photo>
{
    public void Configure(EntityTypeBuilder<Photo> builder)
    {
        builder.ToTable("photo");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.TenantId).IsRequired();
        builder.Property(p => p.GalleryId).IsRequired();
        builder.Property(p => p.OriginalKey).HasMaxLength(500).IsRequired();
        builder.Property(p => p.OriginalHash).HasMaxLength(128).IsRequired();
        builder.Property(p => p.OriginalBytes).IsRequired();
        builder
            .HasMany<PhotoVariant>("_variants")
            .WithOne()
            .HasForeignKey(v => v.PhotoId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation("_variants").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasIndex(p => p.TenantId);
        builder.HasIndex(p => new { p.TenantId, p.GalleryId, p.SortOrder });
        builder.HasIndex(p => new { p.GalleryId, p.OriginalHash }).IsUnique();
        builder.Ignore(p => p.Variants);
        builder.Ignore(p => p.TemThumbEWeb);
    }
}

internal sealed class PhotoVariantConfiguration : IEntityTypeConfiguration<PhotoVariant>
{
    public void Configure(EntityTypeBuilder<PhotoVariant> builder)
    {
        builder.ToTable("photo_variant");
        builder.HasKey(v => v.Id);
        builder.Property(v => v.TenantId).IsRequired();
        builder.Property(v => v.PhotoId).IsRequired();
        builder.Property(v => v.Kind).HasConversion<string>().HasMaxLength(16);
        builder.Property(v => v.StorageKey).HasMaxLength(500).IsRequired();
        builder.HasIndex(v => v.TenantId);
        builder.HasIndex(v => new { v.PhotoId, v.Kind }).IsUnique();
    }
}

internal sealed class SelectionConfiguration : IEntityTypeConfiguration<Selection>
{
    public void Configure(EntityTypeBuilder<Selection> builder)
    {
        builder.ToTable("selection");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.TenantId).IsRequired();
        builder.HasIndex(s => s.TenantId);
        builder.HasIndex(s => new { s.GalleryId, s.PhotoId }).IsUnique();
    }
}

internal sealed class ShareLinkConfiguration : IEntityTypeConfiguration<ShareLink>
{
    public void Configure(EntityTypeBuilder<ShareLink> builder)
    {
        builder.ToTable("share_link");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.TenantId).IsRequired();
        builder.Property(s => s.TokenHash).HasMaxLength(128).IsRequired();
        builder.Property(s => s.PasswordHash).HasMaxLength(128).IsRequired();
        builder.HasIndex(s => s.TenantId);
        builder.HasIndex(s => s.TokenHash).IsUnique();
        builder.Ignore(s => s.DomainEvents);
        builder.Ignore(s => s.PodeBaixarOriginal);
    }
}

internal sealed class DownloadJobConfiguration : IEntityTypeConfiguration<DownloadJob>
{
    public void Configure(EntityTypeBuilder<DownloadJob> builder)
    {
        builder.ToTable("download_job");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.TenantId).IsRequired();
        builder.Property(d => d.Scope).HasMaxLength(64).IsRequired();
        builder.Property(d => d.Status).HasConversion<string>().HasMaxLength(16);
        builder.Property(d => d.StorageKey).HasMaxLength(500);
        builder.HasIndex(d => d.TenantId);
        builder.Ignore(d => d.DomainEvents);
    }
}
