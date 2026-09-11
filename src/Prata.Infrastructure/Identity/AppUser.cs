using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prata.Domain.Common;
using Prata.Domain.Tenancy;
using Prata.Infrastructure.Persistence;

namespace Prata.Infrastructure.Identity;

/// <summary>
/// Usuario Identity com tenant_id. UNIQUE (tenant_id, email) — RN-TEN-004.
/// </summary>
public sealed class AppUser : IdentityUser<Guid>, ITenantOwned
{
    public Guid TenantId { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public bool IsOwner { get; set; }

    public bool IsActive { get; set; } = true;
}

public sealed class AppRole : IdentityRole<Guid>
{
    public AppRole() { }

    public AppRole(string name)
        : base(name) { }
}

internal sealed class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        builder.ToTable("app_user");
        builder.Property(u => u.TenantId).IsRequired();
        builder.Property(u => u.DisplayName).HasMaxLength(200);
        builder.HasIndex(u => new { u.TenantId, u.NormalizedEmail }).IsUnique();
    }
}

internal sealed class AppRoleConfiguration : IEntityTypeConfiguration<AppRole>
{
    public void Configure(EntityTypeBuilder<AppRole> builder)
    {
        builder.ToTable("app_role");
    }
}

internal sealed class RefreshTokenFamilyConfiguration : IEntityTypeConfiguration<RefreshTokenFamily>
{
    public void Configure(EntityTypeBuilder<RefreshTokenFamily> builder)
    {
        builder.ToTable("refresh_token_family");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.TenantId).IsRequired();
        builder.Property(f => f.CurrentTokenHash).HasMaxLength(128).IsRequired();
        builder.HasIndex(f => new { f.TenantId, f.CurrentTokenHash });
        builder.Ignore(f => f.DomainEvents);
    }
}

internal sealed class PasswordResetRequestConfiguration : IEntityTypeConfiguration<PasswordResetRequest>
{
    public void Configure(EntityTypeBuilder<PasswordResetRequest> builder)
    {
        builder.ToTable("password_reset_request");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.TenantId).IsRequired();
        builder.Property(r => r.TokenHash).HasMaxLength(128).IsRequired();
        builder.HasIndex(r => new { r.TenantId, r.TokenHash });
    }
}
