using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pos.Domain.Shops;

namespace Pos.Infrastructure.Persistence.Configurations;

public class ShopConfig : IEntityTypeConfiguration<Shop>
{
    public void Configure(EntityTypeBuilder<Shop> e)
    {
        e.ToTable("shops");
        e.HasKey(x => x.Id);
        e.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        e.Property(x => x.Code).HasMaxLength(32).IsRequired();
        e.Property(x => x.Name).HasMaxLength(256).IsRequired();
        e.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        e.Property(x => x.TimeZoneId).HasMaxLength(64).IsRequired();
        e.Property(x => x.CountryCode).HasMaxLength(2).IsRequired();
        e.Property(x => x.Xmin).IsRowVersion().HasColumnName("xmin").HasColumnType("xid");
    }
}

public class RegisterConfig : IEntityTypeConfiguration<Register>
{
    public void Configure(EntityTypeBuilder<Register> e)
    {
        e.ToTable("registers");
        e.HasKey(x => x.Id);
        e.HasIndex(x => new { x.TenantId, x.ShopId, x.Code }).IsUnique();
        e.Property(x => x.Code).HasMaxLength(32).IsRequired();
        e.Property(x => x.Name).HasMaxLength(128).IsRequired();
        e.Property(x => x.Xmin).IsRowVersion().HasColumnName("xmin").HasColumnType("xid");
    }
}

public class WarehouseConfig : IEntityTypeConfiguration<Warehouse>
{
    public void Configure(EntityTypeBuilder<Warehouse> e)
    {
        e.ToTable("warehouses");
        e.HasKey(x => x.Id);
        e.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        e.Property(x => x.Code).HasMaxLength(32).IsRequired();
        e.Property(x => x.Name).HasMaxLength(128).IsRequired();
        e.Property(x => x.Xmin).IsRowVersion().HasColumnName("xmin").HasColumnType("xid");
    }
}
