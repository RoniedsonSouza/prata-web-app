using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prata.Domain.Contracts;
using Prata.Domain.Scheduling;

namespace Prata.Infrastructure.Persistence.Configurations;

internal sealed class AvailabilityConfiguration : IEntityTypeConfiguration<Availability>
{
    public void Configure(EntityTypeBuilder<Availability> builder)
    {
        builder.ToTable("availability");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.TenantId).IsRequired();
        builder.Property(a => a.Weekday).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(a => a.StartsAtTime).HasColumnType("time").IsRequired();
        builder.Property(a => a.EndsAtTime).HasColumnType("time").IsRequired();
        builder.HasIndex(a => a.TenantId);
        builder.HasIndex(a => new { a.TenantId, a.Weekday });
        builder.Ignore(a => a.DomainEvents);
    }
}

internal sealed class BlackoutDateConfiguration : IEntityTypeConfiguration<BlackoutDate>
{
    public void Configure(EntityTypeBuilder<BlackoutDate> builder)
    {
        builder.ToTable("blackout_date");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.TenantId).IsRequired();
        builder.Property(b => b.Date).IsRequired();
        builder.Property(b => b.Reason).HasMaxLength(500).IsRequired();
        builder.HasIndex(b => new { b.TenantId, b.Date }).IsUnique();
        builder.Ignore(b => b.DomainEvents);
    }
}

internal sealed class ContractConfiguration : IEntityTypeConfiguration<Contract>
{
    public void Configure(EntityTypeBuilder<Contract> builder)
    {
        builder.ToTable("contract");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.TenantId).IsRequired();
        builder.Property(c => c.OrderId).IsRequired();
        builder.Property(c => c.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(c => c.TemplateVersion).HasMaxLength(64).IsRequired();
        builder.Property(c => c.PdfStorageKey).HasMaxLength(500);
        builder.Property(c => c.PdfSha256).HasMaxLength(64);
        builder.Property(c => c.CreatedAt).IsRequired();
        builder.Property(c => c.ExpiresAt).IsRequired();
        builder.Property(c => c.SentAt);
        builder.Property(c => c.ViewedAt);
        builder.Property(c => c.SignedAt);
        builder.Property(c => c.ReplacesContractId);
        builder
            .Property<List<string>>("_clauseCodes")
            .HasColumnName("clause_codes")
            .HasConversion(
                v => string.Join(',', v),
                v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList()
            )
            .HasMaxLength(500)
            .IsRequired();
        builder.Ignore(c => c.ClauseCodes);
        builder.HasOne(c => c.Signature).WithOne().HasForeignKey<Signature>(s => s.ContractId);
        builder.HasIndex(c => c.TenantId);
        builder.HasIndex(c => new { c.TenantId, c.OrderId });
        builder.Ignore(c => c.DomainEvents);
    }
}

internal sealed class SignatureConfiguration : IEntityTypeConfiguration<Signature>
{
    public void Configure(EntityTypeBuilder<Signature> builder)
    {
        builder.ToTable("signature");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.TenantId).IsRequired();
        builder.Property(s => s.ContractId).IsRequired();
        builder.Property(s => s.SignerName).HasMaxLength(200).IsRequired();
        builder.Property(s => s.SignerEmail).HasMaxLength(320).IsRequired();
        builder.Property(s => s.PdfSha256).HasMaxLength(64).IsRequired();
        builder.Property(s => s.Ip).HasMaxLength(64).IsRequired();
        builder.Property(s => s.UserAgent).HasMaxLength(500).IsRequired();
        builder.Property(s => s.SignedAt).IsRequired();
        builder.HasIndex(s => s.TenantId);
        builder.HasIndex(s => s.ContractId).IsUnique();
    }
}
