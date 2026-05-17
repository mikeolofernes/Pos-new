using Pos.Domain.Common;

namespace Pos.Domain.Sales;

public enum SaleStatus : short { Draft = 1, Parked = 2, Completed = 3, Voided = 4, Refunded = 5 }
public enum PaymentStatus : short { Pending = 1, Authorized = 2, Captured = 3, Failed = 4, Refunded = 5 }

public class Sale : TenantEntity
{
    public Guid ShopId { get; set; }
    public Guid RegisterId { get; set; }
    public Guid ShiftId { get; set; }
    public Guid? CustomerId { get; set; }
    public string Number { get; set; } = default!;
    public SaleStatus Status { get; set; } = SaleStatus.Draft;
    public decimal Subtotal { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal Total { get; set; }
    public decimal TenderedTotal { get; set; }
    public decimal ChangeDue { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public Guid CashierId { get; set; }
    public string IdempotencyKey { get; set; } = default!;
    public string? Notes { get; set; }

    public List<SaleItem> Items { get; set; } = new();
    public List<SalePayment> Payments { get; set; } = new();
}

public class SaleItem : TenantEntity
{
    public Guid SaleId { get; set; }
    public int LineNumber { get; set; }
    public Guid ProductId { get; set; }
    public Guid? VariantId { get; set; }
    public string NameSnapshot { get; set; } = default!;
    public string SkuSnapshot { get; set; } = default!;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineDiscount { get; set; }
    public decimal LineTotal { get; set; }
    public Guid? SerialIdSnapshot { get; set; }
    public Guid? BatchIdSnapshot { get; set; }
}

public class SalePayment : TenantEntity
{
    public Guid SaleId { get; set; }
    public string Method { get; set; } = "cash";    // cash, card, wallet, gift_card, store_credit
    public decimal Amount { get; set; }
    public string? ExternalReference { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Captured;
}

public enum ShiftStatus : short { Open = 1, Closed = 2 }

public class Shift : TenantEntity
{
    public Guid ShopId { get; set; }
    public Guid RegisterId { get; set; }
    public Guid OpenedByUserId { get; set; }
    public Guid? ClosedByUserId { get; set; }
    public DateTimeOffset OpenedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ClosedAt { get; set; }
    public ShiftStatus Status { get; set; } = ShiftStatus.Open;
    public decimal OpeningFloat { get; set; }
    public decimal ClosingDeclared { get; set; }
    public decimal ClosingExpected { get; set; }
    public decimal Variance { get; set; }
    public string? Notes { get; set; }
}
