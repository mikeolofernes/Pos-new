using MediatR;
using Microsoft.EntityFrameworkCore;
using Pos.Application.Common.Abstractions;
using Pos.Domain.Tenancy;

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

public sealed record ListMyShopsQuery : IRequest<IReadOnlyList<ShopDto>>;

public class ListMyShopsHandler : IRequestHandler<ListMyShopsQuery, IReadOnlyList<ShopDto>>
{
    private readonly IAppDbContext _db;
    private readonly ITenantContext _tenant;
    public ListMyShopsHandler(IAppDbContext db, ITenantContext tenant) { _db = db; _tenant = tenant; }

    public async Task<IReadOnlyList<ShopDto>> Handle(ListMyShopsQuery req, CancellationToken ct)
    {
        var uid = _tenant.UserId;
        if (uid is null) return Array.Empty<ShopDto>();

        var shopIds = await _db.UserShopRoles.AsNoTracking()
            .Where(r => r.UserId == uid)
            .Select(r => r.ShopId)
            .Distinct()
            .ToListAsync(ct);

        return await _db.Shops.AsNoTracking()
            .Where(s => shopIds.Contains(s.Id) && s.DeletedAt == null)
            .OrderBy(s => s.Name)
            .Select(s => new ShopDto(s.Id, s.Code, s.Name, s.Currency, s.TimeZoneId, s.IsActive))
            .ToListAsync(ct);
    }
}
