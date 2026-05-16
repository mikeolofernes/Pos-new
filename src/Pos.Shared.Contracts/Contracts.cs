namespace Pos.Shared.Contracts;

// ---- Auth ----
public sealed record LoginRequest(string TenantSlug, string Email, string Password, string? DeviceName);

public sealed record LoginResponse(
    string AccessToken, DateTimeOffset AccessExpiresAt,
    string RefreshToken, DateTimeOffset RefreshExpiresAt,
    Guid TenantId, Guid UserId, string DisplayName, string[] Permissions);

public sealed record RefreshRequest(string RefreshToken);

// ---- Shops ----
public sealed record ShopDto(Guid Id, string Code, string Name, string Currency, string TimeZoneId, bool IsActive);

// ---- Products ----
public sealed record ProductDto(
    Guid Id, string Sku, string Name, string? Description, Guid? CategoryId,
    string Type, decimal Price, string Currency, string TaxCode, bool TrackInventory, bool IsActive);

public sealed record CreateProductRequest(
    string Sku, string Name, string? Description, Guid? CategoryId,
    decimal Price, string Currency, string TaxCode, bool TrackInventory);

public sealed record PageOf<T>(IReadOnlyList<T> Items, int Total, int PageNumber, int PageSize);

// ---- Inventory ----
public sealed record BalanceDto(Guid WarehouseId, Guid ProductId, Guid VariantId, decimal OnHand, decimal Reserved, decimal AverageCost);

public sealed record AdjustStockRequest(
    Guid ShopId, Guid WarehouseId, Guid ProductId, Guid? VariantId,
    decimal QuantityDelta, decimal UnitCost, string? Reason);

// ---- POS / Sales ----
public sealed record SaleLineRequest(
    Guid ProductId, Guid? VariantId,
    decimal Quantity, decimal? UnitPriceOverride, decimal LineDiscount);

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

public sealed record OpenShiftRequest(Guid ShopId, Guid RegisterId, decimal OpeningFloat);
public sealed record CloseShiftResponse(Guid ShiftId, decimal Expected, decimal Declared, decimal Variance);

// ---- Problem details ----
public sealed record ApiProblem(string? Title, int? Status, string? Detail, string? Type);

// ---- Sync ----
public sealed record SyncEnvelopeOut(
    string IdempotencyKey, string Resource, string Op,
    object Payload, DateTimeOffset CreatedAt,
    Guid DeviceId, long ClientSeq);

public sealed record SyncEnvelopeResult(string IdempotencyKey, string Status, string? ErrorCode, string? Message, object? Canonical);
public sealed record SyncBatchResponse(IReadOnlyList<SyncEnvelopeResult> Results, DateTimeOffset ServerTime);
