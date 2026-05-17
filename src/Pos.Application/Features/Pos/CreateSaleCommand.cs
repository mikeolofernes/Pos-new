using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pos.Application.Common.Abstractions;
using Pos.Application.Features.Inventory;
using Pos.BuildingBlocks;
using Pos.Domain.Inventory;
using Pos.Domain.Sales;
using Pos.Domain.Tenancy;

namespace Pos.Application.Features.Pos;

public sealed record CreateSaleCommand(CreateSaleRequest Body, string IdempotencyKey)
    : IRequest<Result<SaleSummary>>;

public class CreateSaleValidator : AbstractValidator<CreateSaleCommand>
{
    public CreateSaleValidator()
    {
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(80);
        RuleFor(x => x.Body.ShopId).NotEmpty();
        RuleFor(x => x.Body.RegisterId).NotEmpty();
        RuleFor(x => x.Body.ShiftId).NotEmpty();
        RuleFor(x => x.Body.WarehouseId).NotEmpty();
        RuleFor(x => x.Body.Items).NotEmpty();
        RuleForEach(x => x.Body.Items).ChildRules(i =>
        {
            i.RuleFor(x => x.Quantity).GreaterThan(0);
            i.RuleFor(x => x.ProductId).NotEmpty();
        });
        RuleFor(x => x.Body.Payments).NotEmpty();
        RuleForEach(x => x.Body.Payments).ChildRules(p =>
        {
            p.RuleFor(x => x.Amount).GreaterThan(0);
            p.RuleFor(x => x.Method).NotEmpty();
        });
    }
}

public class CreateSaleHandler : IRequestHandler<CreateSaleCommand, Result<SaleSummary>>
{
    private readonly IAppDbContext _db;
    private readonly IInventoryPostingService _posting;
    private readonly ITenantContext _tenant;

    public CreateSaleHandler(IAppDbContext db, IInventoryPostingService posting, ITenantContext tenant)
    { _db = db; _posting = posting; _tenant = tenant; }

    public async Task<Result<SaleSummary>> Handle(CreateSaleCommand cmd, CancellationToken ct)
    {
        // Idempotency: if the same key was already committed, return the same summary.
        var existing = await _db.Sales.AsNoTracking()
            .FirstOrDefaultAsync(s => s.IdempotencyKey == cmd.IdempotencyKey, ct);
        if (existing is not null)
            return Map(existing);

        var b = cmd.Body;

        var shift = await _db.Shifts.FirstOrDefaultAsync(s => s.Id == b.ShiftId, ct);
        if (shift is null || shift.Status != ShiftStatus.Open)
            return Error.Validation("sale.shift.closed", "Shift is not open");

        // Load products in one query
        var productIds = b.Items.Select(i => i.ProductId).Distinct().ToArray();
        var products = await _db.Products.AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);

        var sale = new Sale
        {
            ShopId = b.ShopId,
            RegisterId = b.RegisterId,
            ShiftId = b.ShiftId,
            CustomerId = b.CustomerId,
            CashierId = _tenant.UserId ?? Guid.Empty,
            Status = SaleStatus.Draft,
            IdempotencyKey = cmd.IdempotencyKey,
            Notes = b.Notes,
            Number = await NextSaleNumberAsync(b.ShopId, b.RegisterId, ct)
        };

        decimal subtotal = 0, discount = 0;
        int line = 1;

        foreach (var i in b.Items)
        {
            if (!products.TryGetValue(i.ProductId, out var p))
                return Error.NotFound("sale.product.missing", $"Product {i.ProductId} not found");

            var unitPrice = i.UnitPriceOverride ?? p.DefaultPriceAmount;
            var grossLine = unitPrice * i.Quantity;
            var netLine = grossLine - i.LineDiscount;

            sale.Items.Add(new SaleItem
            {
                LineNumber = line++,
                ProductId = p.Id,
                VariantId = i.VariantId,
                NameSnapshot = p.Name,
                SkuSnapshot = p.Sku,
                Quantity = i.Quantity,
                UnitPrice = unitPrice,
                LineDiscount = i.LineDiscount,
                LineTotal = netLine
            });

            subtotal += netLine;
            discount += i.LineDiscount;
        }

        var total = subtotal;
        var tendered = b.Payments.Sum(p => p.Amount);
        if (tendered < total)
            return Error.Validation("sale.payment.insufficient",
                $"Tendered {tendered} is less than total {total}");

        sale.Subtotal = subtotal;
        sale.DiscountTotal = discount;
        sale.Total = total;
        sale.TenderedTotal = tendered;
        sale.ChangeDue = tendered - total;
        sale.Status = SaleStatus.Completed;
        sale.CompletedAt = b.ClientCompletedAt ?? DateTimeOffset.UtcNow;

        foreach (var p in b.Payments)
        {
            sale.Payments.Add(new SalePayment
            {
                Method = p.Method,
                Amount = p.Amount,
                ExternalReference = p.ExternalReference,
                Status = PaymentStatus.Captured
            });
        }

        _db.Sales.Add(sale);

        // Post inventory movements + decrement balances
        foreach (var i in sale.Items)
        {
            var p = products[i.ProductId];
            if (!p.TrackInventory) continue;

            await _posting.PostAsync(new PostMovementInput(
                b.ShopId, b.WarehouseId, i.ProductId, i.VariantId,
                MovementType.SaleOut, i.Quantity, p.DefaultCost ?? 0m,
                InventoryReferenceType.Sale, sale.Id,
                _tenant.UserId ?? Guid.Empty, null,
                $"{cmd.IdempotencyKey}:line:{i.LineNumber}"), ct);
        }

        await _db.SaveChangesAsync(ct);

        return Map(sale);
    }

    private async Task<string> NextSaleNumberAsync(Guid shopId, Guid registerId, CancellationToken ct)
    {
        var today = DateTimeOffset.UtcNow.ToString("yyyyMMdd");
        var prefix = $"{shopId.ToString()[..4]}-{registerId.ToString()[..4]}-{today}-";
        var seq = await _db.Sales
            .Where(s => s.ShopId == shopId && s.RegisterId == registerId && s.Number.StartsWith(prefix))
            .CountAsync(ct);
        return $"{prefix}{(seq + 1):D5}";
    }

    private static SaleSummary Map(Sale s) =>
        new(s.Id, s.Number, s.Subtotal, s.DiscountTotal, s.Total,
            s.TenderedTotal, s.ChangeDue, s.Status.ToString(), s.CompletedAt ?? s.CreatedAt);
}
