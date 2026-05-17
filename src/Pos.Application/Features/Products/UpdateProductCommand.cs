using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pos.Application.Common.Abstractions;
using Pos.BuildingBlocks;

namespace Pos.Application.Features.Products;

public sealed record UpdateProductCommand(Guid Id, UpdateProductRequest Body) : IRequest<Result<ProductDto>>;

public class UpdateProductValidator : AbstractValidator<UpdateProductCommand>
{
    public UpdateProductValidator()
    {
        RuleFor(x => x.Body.Name).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Body.Price).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Body.Currency).NotEmpty().Length(3);
        RuleFor(x => x.Body.TaxCode).NotEmpty();
    }
}

public class UpdateProductHandler : IRequestHandler<UpdateProductCommand, Result<ProductDto>>
{
    private readonly IAppDbContext _db;
    public UpdateProductHandler(IAppDbContext db) => _db = db;

    public async Task<Result<ProductDto>> Handle(UpdateProductCommand req, CancellationToken ct)
    {
        var p = await _db.Products.FirstOrDefaultAsync(x => x.Id == req.Id, ct);
        if (p is null) return Error.NotFound("product.not_found", "Product not found");

        var b = req.Body;
        p.Name = b.Name;
        p.Description = b.Description;
        p.CategoryId = b.CategoryId;
        p.DefaultPriceAmount = b.Price;
        p.DefaultPriceCurrency = b.Currency;
        p.TaxCode = b.TaxCode;
        p.ImageUrl = b.ImageUrl;
        p.TrackInventory = b.TrackInventory;
        p.IsActive = b.IsActive;

        await _db.SaveChangesAsync(ct);

        return new ProductDto(p.Id, p.Sku, p.Name, p.Description, p.CategoryId,
            p.Type.ToString(), p.DefaultPriceAmount, p.DefaultPriceCurrency,
            p.TaxCode, p.ImageUrl, p.TrackInventory, p.IsActive);
    }
}

public sealed record DeleteProductCommand(Guid Id) : IRequest<Result<bool>>;

public class DeleteProductHandler : IRequestHandler<DeleteProductCommand, Result<bool>>
{
    private readonly IAppDbContext _db;
    public DeleteProductHandler(IAppDbContext db) => _db = db;

    public async Task<Result<bool>> Handle(DeleteProductCommand req, CancellationToken ct)
    {
        var p = await _db.Products.FirstOrDefaultAsync(x => x.Id == req.Id, ct);
        if (p is null) return Error.NotFound("product.not_found", "Product not found");
        p.DeletedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
