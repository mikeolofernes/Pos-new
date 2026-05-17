using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pos.Domain.Identity;

namespace Pos.Infrastructure.Persistence.Configurations;

public class UserConfig : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> e)
    {
        e.ToTable("users");
        e.HasKey(x => x.Id);
        e.HasIndex(x => x.Email).IsUnique();   // email is globally unique across all tenants
        e.HasIndex(x => x.RoleId);
        e.Property(x => x.Email).HasMaxLength(256).IsRequired();
        e.Property(x => x.DisplayName).HasMaxLength(256).IsRequired();
        e.Property(x => x.PasswordHash).HasMaxLength(512).IsRequired();
    }
}

public class RoleConfig : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> e)
    {
        e.ToTable("roles");
        e.HasKey(x => x.Id);
        e.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
        e.Property(x => x.Name).HasMaxLength(128).IsRequired();
        e.Property(x => x.Permissions).HasColumnType("text[]");
    }
}

public class UserShopRoleConfig : IEntityTypeConfiguration<UserShopRole>
{
    public void Configure(EntityTypeBuilder<UserShopRole> e)
    {
        e.ToTable("user_shop_roles");
        e.HasKey(x => x.Id);
        e.HasIndex(x => new { x.TenantId, x.UserId, x.ShopId, x.RoleId }).IsUnique();
    }
}

public class RefreshTokenConfig : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> e)
    {
        e.ToTable("refresh_tokens");
        e.HasKey(x => x.Id);
        e.HasIndex(x => x.TokenHash).IsUnique();
        e.HasIndex(x => new { x.TenantId, x.UserId });
        e.Property(x => x.TokenHash).HasMaxLength(128).IsRequired();
        e.Property(x => x.ReplacedByHash).HasMaxLength(128);
    }
}

public class DeviceConfig : IEntityTypeConfiguration<Device>
{
    public void Configure(EntityTypeBuilder<Device> e)
    {
        e.ToTable("devices");
        e.HasKey(x => x.Id);
        e.HasIndex(x => new { x.TenantId, x.Fingerprint });
        e.Property(x => x.Name).HasMaxLength(128).IsRequired();
        e.Property(x => x.Type).HasMaxLength(32).IsRequired();
        e.Property(x => x.Fingerprint).HasMaxLength(256);
    }
}
