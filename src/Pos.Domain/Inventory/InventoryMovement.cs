using Pos.Domain.Common;

namespace Pos.Domain.Inventory;

public enum MovementType : short
{
    PurchaseIn = 1, SaleOut = 2, TransferIn = 3, TransferOut = 4,
    AdjustIn = 5, AdjustOut = 6, ReturnIn = 7, ReturnOut = 8,
    ProductionIn = 9, ProductionOut = 10, Damage = 11, Expiry = 12
}

public enum InventoryReferenceType : short
{
    Sale = 1, PurchaseOrder = 2, Transfer = 3, Adjustment = 4, Return = 5, Production = 6
}

public class InventoryMovement : TenantEntity
{
    public Guid ShopId { get; set; }
    public Guid WarehouseId { get; set; }
    public Guid ProductId { get; set; }
    public Guid? VariantId { get; set; }
    public Guid? BatchId { get; set; }
    public Guid? SerialId { get; set; }
    public MovementType MovementType { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public InventoryReferenceType ReferenceType { get; set; }
    public Guid ReferenceId { get; set; }
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid UserId { get; set; }
    public Guid? DeviceId { get; set; }
    public string IdempotencyKey { get; set; } = default!;
}

public class StockBalance
{
    public Guid TenantId { get; set; }
    public Guid WarehouseId { get; set; }
    public Guid ProductId { get; set; }
    public Guid VariantId { get; set; } = Guid.Empty;   // composite key — Guid.Empty when no variant
    public decimal OnHand { get; set; }
    public decimal Reserved { get; set; }
    public decimal AverageCost { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
