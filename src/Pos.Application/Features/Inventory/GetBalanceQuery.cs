using MediatR;
using Microsoft.EntityFrameworkCore;
using Pos.Application.Common.Abstractions;

namespace Pos.Application.Features.Inventory;

public sealed record BalanceDto(Guid WarehouseId, Guid ProductId, Guid VariantId, decimal OnHand, decimal Reserved, decimal AverageCost);

public sealed record GetBalanceQuery(Guid ProductId, Guid? VariantId)
    : IRequest<IReadOnlyList<BalanceDto>>;

public class GetBalanceHandler : IRequestHandler<GetBalanceQuery, IReadOnlyList<BalanceDto>>
{
    private readonly IAppDbContext _db;
    public GetBalanceHandler(IAppDbContext db) => _db = db;

    public async Task<IReadOnlyList<BalanceDto>> Handle(GetBalanceQuery req, CancellationToken ct)
    {
        var variantKey = req.VariantId ?? Guid.Empty;
        return await _db.StockBalances.AsNoTracking()
            .Where(b => b.ProductId == req.ProductId && b.VariantId == variantKey)
            .Select(b => new BalanceDto(b.WarehouseId, b.ProductId, b.VariantId, b.OnHand, b.Reserved, b.AverageCost))
            .ToListAsync(ct);
    }
}
