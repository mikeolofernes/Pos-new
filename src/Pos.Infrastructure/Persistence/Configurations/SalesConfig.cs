using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pos.Domain.Sales;

namespace Pos.Infrastructure.Persistence.Configurations;

public class SaleConfig : IEntityTypeConfiguration<Sale>
{
    public void Configure(EntityTypeBuilder<Sale> e)
    {
        e.ToTable("sales");
        e.HasKey(x => x.Id);
        e.HasIndex(x => new { x.TenantId, x.IdempotencyKey }).IsUnique();
        e.HasIndex(x => new { x.TenantId, x.ShopId, x.CompletedAt });
        e.HasIndex(x => new { x.TenantId, x.ShiftId });
        e.HasIndex(x => x.Number);
        e.Property(x => x.Number).HasMaxLength(64).IsRequired();
        e.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        e.Property(x => x.Status).HasConversion<short>();
        e.Property(x => x.Subtotal).HasColumnType("numeric(19,4)");
        e.Property(x => x.DiscountTotal).HasColumnType("numeric(19,4)");
        e.Property(x => x.TaxTotal).HasColumnType("numeric(19,4)");
        e.Property(x => x.Total).HasColumnType("numeric(19,4)");
        e.Property(x => x.TenderedTotal).HasColumnType("numeric(19,4)");
        e.Property(x => x.ChangeDue).HasColumnType("numeric(19,4)");
        e.Property(x => x.IdempotencyKey).HasMaxLength(128).IsRequired();
        e.HasMany(x => x.Items).WithOne().HasForeignKey(i => i.SaleId).OnDelete(DeleteBehavior.Cascade);
        e.HasMany(x => x.Payments).WithOne().HasForeignKey(p => p.SaleId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class SaleItemConfig : IEntityTypeConfiguration<SaleItem>
{
    public void Configure(EntityTypeBuilder<SaleItem> e)
    {
        e.ToTable("sale_items");
        e.HasKey(x => x.Id);
        e.HasIndex(x => x.SaleId);
        e.Property(x => x.NameSnapshot).HasMaxLength(256).IsRequired();
        e.Property(x => x.SkuSnapshot).HasMaxLength(64).IsRequired();
        e.Property(x => x.Quantity).HasColumnType("numeric(19,4)");
        e.Property(x => x.UnitPrice).HasColumnType("numeric(19,4)");
        e.Property(x => x.LineDiscount).HasColumnType("numeric(19,4)");
        e.Property(x => x.LineTax).HasColumnType("numeric(19,4)");
        e.Property(x => x.LineTotal).HasColumnType("numeric(19,4)");
        e.Property(x => x.TaxRateSnapshot).HasColumnType("numeric(9,6)");
    }
}

public class SalePaymentConfig : IEntityTypeConfiguration<SalePayment>
{
    public void Configure(EntityTypeBuilder<SalePayment> e)
    {
        e.ToTable("sale_payments");
        e.HasKey(x => x.Id);
        e.HasIndex(x => x.SaleId);
        e.Property(x => x.Method).HasMaxLength(32).IsRequired();
        e.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        e.Property(x => x.Amount).HasColumnType("numeric(19,4)");
        e.Property(x => x.FxRate).HasColumnType("numeric(19,8)");
        e.Property(x => x.Status).HasConversion<short>();
        e.Property(x => x.ExternalReference).HasMaxLength(128);
    }
}

public class ShiftConfig : IEntityTypeConfiguration<Shift>
{
    public void Configure(EntityTypeBuilder<Shift> e)
    {
        e.ToTable("shifts");
        e.HasKey(x => x.Id);
        e.HasIndex(x => new { x.TenantId, x.RegisterId, x.Status });
        e.Property(x => x.Status).HasConversion<short>();
        e.Property(x => x.OpeningFloat).HasColumnType("numeric(19,4)");
        e.Property(x => x.ClosingDeclared).HasColumnType("numeric(19,4)");
        e.Property(x => x.ClosingExpected).HasColumnType("numeric(19,4)");
        e.Property(x => x.Variance).HasColumnType("numeric(19,4)");
    }
}
