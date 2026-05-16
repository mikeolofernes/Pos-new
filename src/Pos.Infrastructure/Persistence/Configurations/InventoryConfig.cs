using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pos.Domain.Inventory;

namespace Pos.Infrastructure.Persistence.Configurations;

public class InventoryMovementConfig : IEntityTypeConfiguration<InventoryMovement>
{
    public void Configure(EntityTypeBuilder<InventoryMovement> e)
    {
        e.ToTable("inventory_movements");
        e.HasKey(x => x.Id);
        e.HasIndex(x => new { x.TenantId, x.IdempotencyKey }).IsUnique();
        e.HasIndex(x => new { x.TenantId, x.WarehouseId, x.ProductId, x.VariantId, x.OccurredAt })
         .HasDatabaseName("ix_inv_mov_stock");
        e.HasIndex(x => new { x.TenantId, x.ReferenceType, x.ReferenceId })
         .HasDatabaseName("ix_inv_mov_ref");
        e.Property(x => x.MovementType).HasConversion<short>();
        e.Property(x => x.ReferenceType).HasConversion<short>();
        e.Property(x => x.Quantity).HasColumnType("numeric(19,4)");
        e.Property(x => x.UnitCost).HasColumnType("numeric(19,4)");
        e.Property(x => x.IdempotencyKey).HasMaxLength(128).IsRequired();
    }
}

public class StockBalanceConfig : IEntityTypeConfiguration<StockBalance>
{
    public void Configure(EntityTypeBuilder<StockBalance> e)
    {
        e.ToTable("stock_balances");
        e.HasKey(x => new { x.TenantId, x.WarehouseId, x.ProductId, x.VariantId });
        e.HasIndex(x => new { x.TenantId, x.ProductId });
        e.Property(x => x.OnHand).HasColumnType("numeric(19,4)");
        e.Property(x => x.Reserved).HasColumnType("numeric(19,4)");
        e.Property(x => x.AverageCost).HasColumnType("numeric(19,4)");
    }
}
