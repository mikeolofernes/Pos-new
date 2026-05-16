namespace Pos.Application.Features.Pos;

public sealed record SaleLineRequest(
    Guid ProductId, Guid? VariantId,
    decimal Quantity, decimal? UnitPriceOverride,
    decimal LineDiscount);

public sealed record SalePaymentRequest(string Method, decimal Amount, string? ExternalReference);

public sealed record CreateSaleRequest(
    Guid ShopId, Guid RegisterId, Guid ShiftId, Guid WarehouseId,
    Guid? CustomerId, string Currency,
    IReadOnlyList<SaleLineRequest> Items,
    IReadOnlyList<SalePaymentRequest> Payments,
    string? Notes, DateTimeOffset? ClientCompletedAt);

public sealed record SaleSummary(
    Guid Id, string Number, decimal Subtotal, decimal DiscountTotal,
    decimal TaxTotal, decimal Total, decimal Tendered, decimal Change,
    string Status, DateTimeOffset CompletedAt);
