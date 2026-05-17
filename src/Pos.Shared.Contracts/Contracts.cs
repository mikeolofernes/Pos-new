namespace Pos.Shared.Contracts;

// ---- Auth ----
public sealed record LoginRequest(string TenantSlug, string Email, string Password, string? DeviceName);

public sealed record LoginResponse(
    string AccessToken, DateTimeOffset AccessExpiresAt,
    string RefreshToken, DateTimeOffset RefreshExpiresAt,
    Guid TenantId, Guid UserId, string DisplayName, string[] Permissions);

public sealed record RefreshRequest(string RefreshToken);

// ---- Tenants ----
public sealed record TenantDto(
    Guid Id, string Slug, string Name, string CountryCode, string DefaultCurrency,
    string TimeZoneId, string Status, DateTimeOffset CreatedAt);

public sealed record CreateTenantRequest(
    string Slug, string Name, string CountryCode, string DefaultCurrency, string TimeZoneId,
    string AdminEmail, string AdminDisplayName, string AdminPassword);

// ---- Shops ----
public sealed record ShopDto(Guid Id, string Code, string Name, string Currency, string TimeZoneId, bool IsActive);

public sealed record CreateShopRequest(
    string Code, string Name, string Currency, string TimeZoneId, string CountryCode, Guid? TenantId = null);

public sealed record UpdateShopRequest(
    string Code, string Name, string Currency, string TimeZoneId, string CountryCode, bool IsActive);

// ---- Users ----
public sealed record UserDto(
    Guid Id, string Email, string DisplayName, string? Phone, bool IsActive,
    DateTimeOffset? LastLoginAt, DateTimeOffset CreatedAt,
    Guid? RoleId, string? RoleName, IReadOnlyList<Guid> ShopIds);

public sealed record CreateUserRequest(
    string Email, string DisplayName, string? Phone, string Password, bool IsActive,
    Guid? RoleId = null, IReadOnlyList<Guid>? ShopIds = null);

public sealed record UpdateUserRequest(
    string DisplayName, string? Phone, bool IsActive, string? NewPassword,
    Guid? RoleId = null, IReadOnlyList<Guid>? ShopIds = null);

// ---- Roles ----
public sealed record RoleDto(Guid Id, string Name, IReadOnlyList<string> Permissions, bool IsSystem);

// ---- Categories ----
public sealed record CategoryDto(Guid Id, Guid? ParentId, string Name, string Slug);
public sealed record CreateCategoryRequest(Guid? ParentId, string Name, string Slug);
public sealed record UpdateCategoryRequest(Guid? ParentId, string Name, string Slug);

// ---- Products ----
public sealed record ProductDto(
    Guid Id, string Sku, string Name, string? Description, Guid? CategoryId,
    string Type, decimal Price, string Currency, string TaxCode,
    string? ImageUrl, bool TrackInventory, bool IsActive);

public sealed record CreateProductRequest(
    string Sku, string Name, string? Description, Guid? CategoryId,
    decimal Price, string Currency, string TaxCode, string? ImageUrl, bool TrackInventory);

public sealed record UpdateProductRequest(
    string Name, string? Description, Guid? CategoryId,
    decimal Price, string Currency, string TaxCode, string? ImageUrl,
    bool TrackInventory, bool IsActive);

// ---- Quick selects (POS shortcut tiles) ----
public sealed record QuickSelectDto(
    Guid Id, Guid ShopId, Guid ProductId, int Position,
    string? Label, string? Color,
    string ProductSku, string ProductName, decimal Price, string Currency, string? ImageUrl);

public sealed record CreateQuickSelectRequest(Guid ProductId, int? Position, string? Label, string? Color);
public sealed record UpdateQuickSelectRequest(int Position, string? Label, string? Color);

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
