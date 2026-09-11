using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prata.Domain.Billing;
using Prata.Domain.Common;

namespace Prata.Infrastructure.Persistence.Configurations;

internal sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("payment");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.TenantId).IsRequired();
        builder.Property(p => p.OrderId).IsRequired();
        builder.Property(p => p.Kind).HasConversion<string>().HasMaxLength(32);
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(p => p.Method).HasConversion<string>().HasMaxLength(16);
        builder.Property(p => p.ExternalChargeId).HasMaxLength(120);
        builder.Property(p => p.CreatedAt).IsRequired();

        builder
            .Property(p => p.Total)
            .HasConversion(m => m.Amount, a => Money.Brl(a))
            .HasColumnName("total_amount")
            .HasColumnType("numeric(14,2)")
            .IsRequired();

        builder
            .Property(p => p.PlatformFeeAmount)
            .HasConversion(m => m.Amount, a => Money.Brl(a))
            .HasColumnName("platform_fee_amount")
            .HasColumnType("numeric(14,2)")
            .IsRequired();

        builder.OwnsOne(
            p => p.SplitSnapshot,
            snap =>
            {
                snap.Property(s => s.Percent).HasColumnName("split_percent").HasColumnType("numeric(5,2)");
                snap
                    .Property(s => s.FixedAmount)
                    .HasConversion(
                        m => m.HasValue ? m.Value.Amount : (decimal?)null,
                        a => a.HasValue ? Money.Brl(a.Value) : null
                    )
                    .HasColumnName("split_fixed_amount")
                    .HasColumnType("numeric(14,2)");
                snap
                    .Property(s => s.PlatformFee)
                    .HasConversion(m => m.Amount, a => Money.Brl(a))
                    .HasColumnName("split_platform_fee_amount")
                    .HasColumnType("numeric(14,2)");
            }
        );

        builder
            .HasMany<Installment>("_installments")
            .WithOne()
            .HasForeignKey(i => i.PaymentId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation("_installments").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(p => p.TenantId);
        builder.HasIndex(p => new { p.TenantId, p.OrderId });
        builder.HasIndex(p => new { p.TenantId, p.Status });
        builder.Ignore(p => p.DomainEvents);
        builder.Ignore(p => p.Installments);
        builder.Ignore(p => p.SinalConfirmado);
    }
}

internal sealed class InstallmentConfiguration : IEntityTypeConfiguration<Installment>
{
    public void Configure(EntityTypeBuilder<Installment> builder)
    {
        builder.ToTable("installment");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.TenantId).IsRequired();
        builder.Property(i => i.PaymentId).IsRequired();
        builder.Property(i => i.Sequence).IsRequired();
        builder.Property(i => i.DueDate).IsRequired();
        builder.Property(i => i.Method).HasConversion<string>().HasMaxLength(16);
        builder.Property(i => i.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(i => i.ExternalInstallmentId).HasMaxLength(120);
        builder
            .Property(i => i.Amount)
            .HasConversion(m => m.Amount, a => Money.Brl(a))
            .HasColumnName("amount")
            .HasColumnType("numeric(14,2)")
            .IsRequired();
        builder.HasIndex(i => i.TenantId);
        builder.HasIndex(i => new { i.PaymentId, i.Sequence }).IsUnique();
    }
}

internal sealed class SplitRuleConfiguration : IEntityTypeConfiguration<SplitRule>
{
    public void Configure(EntityTypeBuilder<SplitRule> builder)
    {
        builder.ToTable("split_rule");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.TenantId).IsRequired();
        builder.Property(s => s.Percent).HasColumnType("numeric(5,2)");
        builder
            .Property(s => s.FixedAmount)
            .HasConversion(
                m => m.HasValue ? m.Value.Amount : (decimal?)null,
                a => a.HasValue ? Money.Brl(a.Value) : null
            )
            .HasColumnName("fixed_amount")
            .HasColumnType("numeric(14,2)");
        builder.Property(s => s.VigenteDe).IsRequired();
        builder.Property(s => s.VigenteAte);
        builder.Property(s => s.CreatedBy).IsRequired();
        builder.HasIndex(s => s.TenantId);
        builder.Ignore(s => s.DomainEvents);
    }
}

internal sealed class PayoutAccountConfiguration : IEntityTypeConfiguration<PayoutAccount>
{
    public void Configure(EntityTypeBuilder<PayoutAccount> builder)
    {
        builder.ToTable("payout_account");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.TenantId).IsRequired();
        builder.Property(p => p.ExternalRecipientId).HasMaxLength(120).IsRequired();
        builder.Property(p => p.KycStatus).HasConversion<string>().HasMaxLength(32);
        builder.Property(p => p.HolderDocumentMasked).HasMaxLength(32);
        builder.Property(p => p.PixKeyMasked).HasMaxLength(64);
        builder.Property(p => p.BankMasked).HasMaxLength(64);
        builder.HasIndex(p => p.TenantId).IsUnique();
        builder.Ignore(p => p.DomainEvents);
    }
}

internal sealed class PayoutConfiguration : IEntityTypeConfiguration<Payout>
{
    public void Configure(EntityTypeBuilder<Payout> builder)
    {
        builder.ToTable("payout");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.TenantId).IsRequired();
        builder.Property(p => p.PaymentId).IsRequired();
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(32);
        builder
            .Property(p => p.Amount)
            .HasConversion(m => m.Amount, a => Money.Brl(a))
            .HasColumnName("amount")
            .HasColumnType("numeric(14,2)");
        builder.Property(p => p.FailureReason).HasMaxLength(500);
        builder.Property(p => p.ExternalPayoutId).HasMaxLength(120);
        builder.HasIndex(p => p.TenantId);
        builder.HasIndex(p => new { p.TenantId, p.Status });
        builder.Ignore(p => p.DomainEvents);
    }
}

internal sealed class PaymentEventConfiguration : IEntityTypeConfiguration<PaymentEvent>
{
    public void Configure(EntityTypeBuilder<PaymentEvent> builder)
    {
        builder.ToTable("payment_event");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.TenantId).IsRequired();
        builder.Property(e => e.ExternalEventId).HasMaxLength(200).IsRequired();
        builder.Property(e => e.EventType).HasMaxLength(100).IsRequired();
        builder.Property(e => e.PayloadJson).HasColumnType("jsonb").IsRequired();
        builder.HasIndex(e => e.ExternalEventId).IsUnique();
        builder.HasIndex(e => e.TenantId);
    }
}

internal sealed class ReconciliationIssueConfiguration : IEntityTypeConfiguration<ReconciliationIssue>
{
    public void Configure(EntityTypeBuilder<ReconciliationIssue> builder)
    {
        builder.ToTable("reconciliation_issue");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.TenantId).IsRequired();
        builder.Property(r => r.Kind).HasMaxLength(64).IsRequired();
        builder.Property(r => r.ExpectedJson).HasColumnType("jsonb").IsRequired();
        builder.Property(r => r.FoundJson).HasColumnType("jsonb").IsRequired();
        builder.Property(r => r.ResolutionNote).HasMaxLength(2000);
        builder.HasIndex(r => r.TenantId);
        builder.HasIndex(r => new { r.TenantId, r.ResolvedAt });
        builder.Ignore(r => r.DomainEvents);
    }
}

internal sealed class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.ToTable("booking");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.TenantId).IsRequired();
        builder.Property(b => b.OrderId).IsRequired();
        builder.Property(b => b.StartsAt).IsRequired();
        builder.Property(b => b.EndsAt).IsRequired();
        builder.Property(b => b.TravelBufferMinutes).IsRequired();
        builder.Property(b => b.Status).HasConversion<string>().HasMaxLength(16);
        builder.HasIndex(b => b.TenantId);
        builder.HasIndex(b => new { b.TenantId, b.OrderId }).IsUnique();
        builder.Ignore(b => b.DomainEvents);
    }
}
