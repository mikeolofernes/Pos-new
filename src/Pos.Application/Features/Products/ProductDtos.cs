namespace Pos.Application.Features.Products;

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
