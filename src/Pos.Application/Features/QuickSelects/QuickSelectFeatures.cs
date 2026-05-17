using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pos.Application.Common.Abstractions;
using Pos.BuildingBlocks;
using Pos.Domain.Catalog;

namespace Pos.Application.Features.QuickSelects;

public sealed record QuickSelectDto(
    Guid Id, Guid ShopId, Guid ProductId, int Position,
    string? Label, string? Color,
    string ProductSku, string ProductName, decimal Price, string? ImageUrl);

public sealed record CreateQuickSelectRequest(Guid ProductId, int? Position, string? Label, string? Color);
public sealed record UpdateQuickSelectRequest(int Position, string? Label, string? Color);

public sealed record ListQuickSelectsQuery : IRequest<IReadOnlyList<QuickSelectDto>>;
public sealed record CreateQuickSelectCommand(CreateQuickSelectRequest Body) : IRequest<Result<QuickSelectDto>>;
public sealed record UpdateQuickSelectCommand(Guid Id, UpdateQuickSelectRequest Body) : IRequest<Result<QuickSelectDto>>;
public sealed record DeleteQuickSelectCommand(Guid Id) : IRequest<Result<bool>>;

public class CreateQuickSelectValidator : AbstractValidator<CreateQuickSelectCommand>
{
    public CreateQuickSelectValidator()
    {
        RuleFor(x => x.Body.ProductId).NotEmpty();
        RuleFor(x => x.Body.Label!).MaximumLength(64).When(x => !string.IsNullOrEmpty(x.Body.Label));
        RuleFor(x => x.Body.Color!).MaximumLength(16).When(x => !string.IsNullOrEmpty(x.Body.Color));
    }
}

public class ListQuickSelectsHandler : IRequestHandler<ListQuickSelectsQuery, IReadOnlyList<QuickSelectDto>>
{
    private readonly IAppDbContext _db;
    public ListQuickSelectsHandler(IAppDbContext db) => _db = db;

    public async Task<IReadOnlyList<QuickSelectDto>> Handle(ListQuickSelectsQuery req, CancellationToken ct)
    {
        // Query filters in AppDbContext scope both sides to current tenant + shop.
        var q = from qs in _db.QuickSelects.AsNoTracking()
                join p in _db.Products.AsNoTracking() on qs.ProductId equals p.Id
                where p.IsActive
                orderby qs.Position, qs.Id
                select new QuickSelectDto(
                    qs.Id, qs.ShopId, qs.ProductId, qs.Position,
                    qs.Label, qs.Color,
                    p.Sku, p.Name, p.DefaultPriceAmount, p.ImageUrl);
        return await q.ToListAsync(ct);
    }
}

public class CreateQuickSelectHandler : IRequestHandler<CreateQuickSelectCommand, Result<QuickSelectDto>>
{
    private readonly IAppDbContext _db;
    public CreateQuickSelectHandler(IAppDbContext db) => _db = db;

    public async Task<Result<QuickSelectDto>> Handle(CreateQuickSelectCommand req, CancellationToken ct)
    {
        var b = req.Body;
        var p = await _db.Products.AsNoTracking().FirstOrDefaultAsync(x => x.Id == b.ProductId, ct);
        if (p is null) return Error.NotFound("product.not_found", "Product not found");

        var dup = await _db.QuickSelects.AnyAsync(x => x.ProductId == b.ProductId, ct);
        if (dup) return Error.Conflict("quickselect.duplicate", "That product is already pinned");

        var pos = b.Position ?? (await _db.QuickSelects
            .Select(x => (int?)x.Position).MaxAsync(ct) ?? -1) + 1;

        var qs = new QuickSelect
        {
            ProductId = b.ProductId,
            Position = pos,
            Label = b.Label,
            Color = b.Color
            // TenantId + ShopId populated by AppDbContext.SaveChangesAsync from the request context.
        };
        _db.QuickSelects.Add(qs);
        await _db.SaveChangesAsync(ct);

        return new QuickSelectDto(qs.Id, qs.ShopId, qs.ProductId, qs.Position,
            qs.Label, qs.Color,
            p.Sku, p.Name, p.DefaultPriceAmount, p.ImageUrl);
    }
}

public class UpdateQuickSelectHandler : IRequestHandler<UpdateQuickSelectCommand, Result<QuickSelectDto>>
{
    private readonly IAppDbContext _db;
    public UpdateQuickSelectHandler(IAppDbContext db) => _db = db;

    public async Task<Result<QuickSelectDto>> Handle(UpdateQuickSelectCommand req, CancellationToken ct)
    {
        var qs = await _db.QuickSelects.FirstOrDefaultAsync(x => x.Id == req.Id, ct);
        if (qs is null) return Error.NotFound("quickselect.not_found", "Tile not found");
        qs.Position = req.Body.Position;
        qs.Label = req.Body.Label;
        qs.Color = req.Body.Color;
        await _db.SaveChangesAsync(ct);

        var p = await _db.Products.AsNoTracking().FirstAsync(x => x.Id == qs.ProductId, ct);
        return new QuickSelectDto(qs.Id, qs.ShopId, qs.ProductId, qs.Position,
            qs.Label, qs.Color,
            p.Sku, p.Name, p.DefaultPriceAmount, p.ImageUrl);
    }
}

public class DeleteQuickSelectHandler : IRequestHandler<DeleteQuickSelectCommand, Result<bool>>
{
    private readonly IAppDbContext _db;
    public DeleteQuickSelectHandler(IAppDbContext db) => _db = db;

    public async Task<Result<bool>> Handle(DeleteQuickSelectCommand req, CancellationToken ct)
    {
        var qs = await _db.QuickSelects.FirstOrDefaultAsync(x => x.Id == req.Id, ct);
        if (qs is null) return Error.NotFound("quickselect.not_found", "Tile not found");
        qs.DeletedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
