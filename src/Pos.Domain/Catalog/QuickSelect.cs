using Pos.Domain.Common;

namespace Pos.Domain.Catalog;

/// <summary>
/// A shortcut tile shown on the POS page so cashiers can punch frequent
/// products with one tap. Per-tenant; optionally scoped to a single shop.
/// </summary>
public class QuickSelect : TenantEntity
{
    public Guid? ShopId { get; set; }
    public Guid ProductId { get; set; }
    public int Position { get; set; }
    public string? Label { get; set; }
    public string? Color { get; set; }
}
