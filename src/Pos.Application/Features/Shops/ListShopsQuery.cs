using MediatR;
using Microsoft.EntityFrameworkCore;
using Pos.Application.Common.Abstractions;

namespace Pos.Application.Features.Shops;

public sealed record ShopDto(Guid Id, string Code, string Name, string Currency, string TimeZoneId, bool IsActive);

public sealed record ListShopsQuery : IRequest<IReadOnlyList<ShopDto>>;

public class ListShopsHandler : IRequestHandler<ListShopsQuery, IReadOnlyList<ShopDto>>
{
    private readonly IAppDbContext _db;
    public ListShopsHandler(IAppDbContext db) => _db = db;

    public async Task<IReadOnlyList<ShopDto>> Handle(ListShopsQuery req, CancellationToken ct)
    {
        return await _db.Shops.AsNoTracking()
            .Where(s => s.DeletedAt == null)
            .OrderBy(s => s.Name)
            .Select(s => new ShopDto(s.Id, s.Code, s.Name, s.Currency, s.TimeZoneId, s.IsActive))
            .ToListAsync(ct);
    }
}
