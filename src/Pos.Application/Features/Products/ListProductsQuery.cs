using MediatR;
using Microsoft.EntityFrameworkCore;
using Pos.Application.Common.Abstractions;
using Pos.BuildingBlocks;

namespace Pos.Application.Features.Products;

public sealed record ListProductsQuery(string? Q, int Page = 1, int PageSize = 50)
    : IRequest<Page<ProductDto>>;

public class ListProductsHandler : IRequestHandler<ListProductsQuery, Page<ProductDto>>
{
    private readonly IAppDbContext _db;
    public ListProductsHandler(IAppDbContext db) => _db = db;

    public async Task<Page<ProductDto>> Handle(ListProductsQuery req, CancellationToken ct)
    {
        var page = new PageRequest(req.Page, req.PageSize, Q: req.Q);

        var query = _db.Products.AsNoTracking().Where(p => p.DeletedAt == null);
        if (!string.IsNullOrWhiteSpace(req.Q))
        {
            // EF Core 9 + Npgsql translates ToLower + Contains to a case-insensitive
            // LIKE / ILIKE depending on collation. Keeps Pos.Application provider-agnostic.
            var q = req.Q.Trim().ToLowerInvariant();
            query = query.Where(p => p.Name.ToLower().Contains(q) || p.Sku.ToLower().Contains(q));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(p => p.Name)
            .Skip(page.Skip).Take(page.Take)
            .Select(p => new ProductDto(
                p.Id, p.Sku, p.Name, p.Description, p.CategoryId,
                p.Type.ToString(), p.DefaultPriceAmount, p.DefaultPriceCurrency,
                p.TaxCode, p.TrackInventory, p.IsActive))
            .ToListAsync(ct);

        return new Page<ProductDto>(items, total, page.Page, page.Take);
    }
}
