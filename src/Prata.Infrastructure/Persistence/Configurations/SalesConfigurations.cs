using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prata.Domain.Common;
using Prata.Domain.Sales;

namespace Prata.Infrastructure.Persistence.Configurations;

internal sealed class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> builder)
    {
        builder.ToTable("client");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.TenantId).IsRequired();
        builder.Property(c => c.Name).HasMaxLength(200).IsRequired();
        builder.Property(c => c.Email).HasMaxLength(320).IsRequired();
        builder.Property(c => c.WhatsApp).HasMaxLength(32);
        builder.Property(c => c.PreferredChannel).HasConversion<string>().HasMaxLength(32);
        builder.HasIndex(c => new { c.TenantId, c.Email }).IsUnique();
        builder.HasIndex(c => c.TenantId);
        builder.Ignore(c => c.DomainEvents);
    }
}

internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("order");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.TenantId).IsRequired();
        builder.Property(o => o.ClientId).IsRequired();
        builder.Property(o => o.ServiceTypeId).IsRequired();
        builder.Property(o => o.IntendedDate).IsRequired();
        builder.Property(o => o.ScheduledStartsAt);
        builder.Property(o => o.ScheduledEndsAt);
        builder.Property(o => o.TravelBufferMinutes);
        builder.Property(o => o.CreatedAt).IsRequired();
        builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(o => o.StatusAntesDaEspera).HasConversion<string>().HasMaxLength(32);
        builder.Property(o => o.HoldReason).HasMaxLength(1000);
        builder.Property(o => o.RefusalReason).HasMaxLength(1000);
        builder.Property(o => o.CancellationReason).HasMaxLength(1000);

        builder
            .Property(o => o.Subtotal)
            .HasConversion(money => money.Amount, amount => Money.Brl(amount))
            .HasColumnName("subtotal_amount")
            .HasColumnType("numeric(14,2)")
            .IsRequired();

        builder
            .Property(o => o.DiscountAmount)
            .HasConversion(money => money.Amount, amount => Money.Brl(amount))
            .HasColumnName("discount_amount")
            .HasColumnType("numeric(14,2)")
            .IsRequired();

        builder
            .Property(o => o.Total)
            .HasConversion(money => money.Amount, amount => Money.Brl(amount))
            .HasColumnName("total_amount")
            .HasColumnType("numeric(14,2)")
            .IsRequired();

        builder.OwnsOne(
            o => o.Discount,
            discount =>
            {
                discount.Property(d => d.Kind).HasConversion<string>().HasColumnName("discount_kind").HasMaxLength(16);
                discount.Property(d => d.Percent).HasColumnName("discount_percent").HasColumnType("numeric(5,2)");
                discount
                    .Property(d => d.FixedAmount)
                    .HasConversion(
                        money => money.HasValue ? money.Value.Amount : (decimal?)null,
                        amount => amount.HasValue ? Money.Brl(amount.Value) : null
                    )
                    .HasColumnName("discount_fixed_amount")
                    .HasColumnType("numeric(14,2)");
            }
        );

        builder
            .HasMany<OrderItem>("_items")
            .WithOne()
            .HasForeignKey(i => i.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasMany<Quote>("_quotes")
            .WithOne()
            .HasForeignKey(q => q.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation("_items").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation("_quotes").UsePropertyAccessMode(PropertyAccessMode.Field);

        var jsonOptions = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);
        builder
            .Property<List<Guid>>("_sensitiveStaffUserIds")
            .HasField("_sensitiveStaffUserIds")
            .HasColumnName("sensitive_staff_user_ids")
            .HasColumnType("jsonb")
            .HasConversion(
                v => System.Text.Json.JsonSerializer.Serialize(v, jsonOptions),
                v =>
                    System.Text.Json.JsonSerializer.Deserialize<List<Guid>>(v, jsonOptions)
                    ?? new List<Guid>()
            );

        builder.HasIndex(o => o.TenantId);
        builder.HasIndex(o => new { o.TenantId, o.Status });
        builder.HasIndex(o => new { o.TenantId, o.IntendedDate });
        builder.Ignore(o => o.DomainEvents);
        builder.Ignore(o => o.Items);
        builder.Ignore(o => o.Quotes);
        builder.Ignore(o => o.CurrentQuote);
        builder.Ignore(o => o.SensitiveStaffUserIds);
    }
}

internal sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("order_item");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.TenantId).IsRequired();
        builder.Property(i => i.OrderId).IsRequired();
        builder.Property(i => i.Kind).HasConversion<string>().HasMaxLength(16);
        builder.Property(i => i.CatalogItemId).IsRequired();
        builder.Property(i => i.NameSnapshot).HasMaxLength(200).IsRequired();
        builder
            .Property(i => i.UnitPriceSnapshot)
            .HasConversion(money => money.Amount, amount => Money.Brl(amount))
            .HasColumnName("unit_price_amount")
            .HasColumnType("numeric(14,2)")
            .IsRequired();
        builder.Property(i => i.Quantity).IsRequired();
        builder.HasIndex(i => i.TenantId);
        builder.HasIndex(i => new { i.TenantId, i.OrderId });
        builder.Ignore(i => i.LineTotal);
    }
}

internal sealed class QuoteConfiguration : IEntityTypeConfiguration<Quote>
{
    public void Configure(EntityTypeBuilder<Quote> builder)
    {
        builder.ToTable("quote");
        builder.HasKey(q => q.Id);
        builder.Property(q => q.TenantId).IsRequired();
        builder.Property(q => q.OrderId).IsRequired();
        builder.Property(q => q.Version).IsRequired();
        builder.Property(q => q.EmittedAt).IsRequired();
        builder.Property(q => q.ValidoAte).IsRequired();
        builder
            .Property(q => q.TotalSnapshot)
            .HasConversion(money => money.Amount, amount => Money.Brl(amount))
            .HasColumnName("total_amount")
            .HasColumnType("numeric(14,2)")
            .IsRequired();
        builder.HasIndex(q => q.TenantId);
        builder.HasIndex(q => new { q.TenantId, q.OrderId, q.Version }).IsUnique();
    }
}
