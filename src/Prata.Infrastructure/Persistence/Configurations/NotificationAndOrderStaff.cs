using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prata.Domain.Notifications;

namespace Prata.Infrastructure.Persistence.Configurations;

internal sealed class NotificationMessageConfiguration : IEntityTypeConfiguration<NotificationMessage>
{
    public void Configure(EntityTypeBuilder<NotificationMessage> builder)
    {
        builder.ToTable("notification_message");
        builder.HasKey(n => n.Id);
        builder.Property(n => n.TenantId).IsRequired();
        builder.Property(n => n.Recipient).HasMaxLength(320).IsRequired();
        builder.Property(n => n.Type).HasMaxLength(64).IsRequired();
        builder.Property(n => n.IdempotencyKey).HasMaxLength(128).IsRequired();
        builder.Property(n => n.Subject).HasMaxLength(500).IsRequired();
        builder.Property(n => n.CreatedAt).IsRequired();
        builder.Property(n => n.LastError).HasMaxLength(500);
        builder.HasIndex(n => n.TenantId);
        builder
            .HasIndex(n => new { n.TenantId, n.Recipient, n.Type, n.IdempotencyKey })
            .IsUnique();
    }
}
