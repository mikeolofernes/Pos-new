using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pos.Application.Common.Abstractions;
using Pos.BuildingBlocks;
using Pos.Domain.Inventory;
using Pos.Domain.Tenancy;

namespace Pos.Application.Features.Inventory;

public sealed record AdjustStockCommand(
    Guid ShopId, Guid WarehouseId, Guid ProductId, Guid? VariantId,
    decimal QuantityDelta, decimal UnitCost, string? Reason, string IdempotencyKey)
    : IRequest<Result<AdjustStockResult>>;

public sealed record AdjustStockResult(Guid MovementId, decimal NewOnHand, decimal AverageCost);

public class AdjustStockValidator : AbstractValidator<AdjustStockCommand>
{
    public AdjustStockValidator()
    {
        RuleFor(x => x.ShopId).NotEmpty();
        RuleFor(x => x.WarehouseId).NotEmpty();
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.QuantityDelta).NotEqual(0);
        RuleFor(x => x.UnitCost).GreaterThanOrEqualTo(0);
        RuleFor(x => x.IdempotencyKey).NotEmpty();
    }
}

public class AdjustStockHandler : IRequestHandler<AdjustStockCommand, Result<AdjustStockResult>>
{
    private readonly IAppDbContext _db;
    private readonly IInventoryPostingService _posting;
    private readonly ITenantContext _tenant;

    public AdjustStockHandler(IAppDbContext db, IInventoryPostingService posting, ITenantContext tenant)
    { _db = db; _posting = posting; _tenant = tenant; }

    public async Task<Result<AdjustStockResult>> Handle(AdjustStockCommand req, CancellationToken ct)
    {
        var dup = await _db.InventoryMovements
            .Where(m => m.IdempotencyKey == req.IdempotencyKey)
            .Select(m => new { m.Id })
            .FirstOrDefaultAsync(ct);
        if (dup is not null)
        {
            var b = await _db.StockBalances.AsNoTracking()
                .FirstAsync(x => x.WarehouseId == req.WarehouseId
                              && x.ProductId == req.ProductId
                              && x.VariantId == (req.VariantId ?? Guid.Empty), ct);
            return new AdjustStockResult(dup.Id, b.OnHand, b.AverageCost);
        }

        var type = req.QuantityDelta > 0 ? MovementType.AdjustIn : MovementType.AdjustOut;
        await _posting.PostAsync(new PostMovementInput(
            req.ShopId, req.WarehouseId, req.ProductId, req.VariantId,
            type, Math.Abs(req.QuantityDelta), req.UnitCost,
            InventoryReferenceType.Adjustment, Guid.NewGuid(),
            _tenant.UserId ?? Guid.Empty, null, req.IdempotencyKey), ct);

        await _db.SaveChangesAsync(ct);

        var bal = await _db.StockBalances.AsNoTracking()
            .FirstAsync(x => x.WarehouseId == req.WarehouseId
                          && x.ProductId == req.ProductId
                          && x.VariantId == (req.VariantId ?? Guid.Empty), ct);

        var mv = await _db.InventoryMovements.AsNoTracking()
            .Where(m => m.IdempotencyKey == req.IdempotencyKey)
            .Select(m => m.Id).FirstAsync(ct);

        return new AdjustStockResult(mv, bal.OnHand, bal.AverageCost);
    }
}
