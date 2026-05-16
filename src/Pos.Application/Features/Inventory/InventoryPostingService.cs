using Microsoft.EntityFrameworkCore;
using Pos.Application.Common.Abstractions;
using Pos.Domain.Common;
using Pos.Domain.Inventory;
using Pos.Domain.Tenancy;

namespace Pos.Application.Features.Inventory;

public sealed record PostMovementInput(
    Guid ShopId, Guid WarehouseId, Guid ProductId, Guid? VariantId,
    MovementType Type, decimal Quantity, decimal UnitCost,
    InventoryReferenceType ReferenceType, Guid ReferenceId,
    Guid UserId, Guid? DeviceId, string IdempotencyKey);

public interface IInventoryPostingService
{
    Task PostAsync(PostMovementInput input, CancellationToken ct);
}

/// <summary>
/// Append-only ledger writer. Updates the StockBalance cache transactionally.
/// Caller is responsible for the surrounding DB transaction.
/// </summary>
public class InventoryPostingService : IInventoryPostingService
{
    private readonly IAppDbContext _db;
    private readonly ITenantContext _tenant;
    public InventoryPostingService(IAppDbContext db, ITenantContext tenant) { _db = db; _tenant = tenant; }

    public async Task PostAsync(PostMovementInput i, CancellationToken ct)
    {
        if (i.Quantity == 0) throw new DomainException("inventory.qty.zero", "Quantity must not be zero");

        var signedQty = i.Type switch
        {
            MovementType.PurchaseIn or MovementType.TransferIn or MovementType.AdjustIn
                or MovementType.ReturnIn or MovementType.ProductionIn => Math.Abs(i.Quantity),
            _ => -Math.Abs(i.Quantity)
        };

        var variantKey = i.VariantId ?? Guid.Empty;
        var balance = await _db.StockBalances
            .FirstOrDefaultAsync(b => b.WarehouseId == i.WarehouseId
                                   && b.ProductId == i.ProductId
                                   && b.VariantId == variantKey, ct);

        if (balance is null)
        {
            balance = new StockBalance
            {
                TenantId = _tenant.TenantId,
                WarehouseId = i.WarehouseId,
                ProductId = i.ProductId,
                VariantId = variantKey,
                OnHand = 0,
                AverageCost = i.UnitCost
            };
            _db.StockBalances.Add(balance);
        }

        var newOnHand = balance.OnHand + signedQty;
        if (newOnHand < 0)
            throw new DomainException("inventory.oversell",
                $"Insufficient stock: on_hand={balance.OnHand}, requested={Math.Abs(signedQty)}");

        // Moving average cost — only updated on inbound movements
        if (signedQty > 0 && i.UnitCost > 0)
        {
            var totalCost = (balance.OnHand * balance.AverageCost) + (signedQty * i.UnitCost);
            balance.AverageCost = newOnHand == 0 ? i.UnitCost : Math.Round(totalCost / newOnHand, 4, MidpointRounding.AwayFromZero);
        }

        balance.OnHand = newOnHand;
        balance.UpdatedAt = DateTimeOffset.UtcNow;

        _db.InventoryMovements.Add(new InventoryMovement
        {
            ShopId = i.ShopId,
            WarehouseId = i.WarehouseId,
            ProductId = i.ProductId,
            VariantId = i.VariantId,
            MovementType = i.Type,
            Quantity = signedQty,
            UnitCost = i.UnitCost == 0 ? balance.AverageCost : i.UnitCost,
            ReferenceType = i.ReferenceType,
            ReferenceId = i.ReferenceId,
            UserId = i.UserId,
            DeviceId = i.DeviceId,
            IdempotencyKey = i.IdempotencyKey,
            OccurredAt = DateTimeOffset.UtcNow
        });
    }
}
