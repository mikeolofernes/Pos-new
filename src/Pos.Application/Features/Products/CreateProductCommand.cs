using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pos.Application.Common.Abstractions;
using Pos.BuildingBlocks;
using Pos.Domain.Catalog;

namespace Pos.Application.Features.Products;

public sealed record CreateProductCommand(CreateProductRequest Body) : IRequest<Result<ProductDto>>;

public class CreateProductValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductValidator()
    {
        RuleFor(x => x.Body.Sku).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Body.Name).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Body.Price).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Body.Currency).NotEmpty().Length(3);
    }
}

public class CreateProductHandler : IRequestHandler<CreateProductCommand, Result<ProductDto>>
{
    private readonly IAppDbContext _db;
    public CreateProductHandler(IAppDbContext db) => _db = db;

    public async Task<Result<ProductDto>> Handle(CreateProductCommand req, CancellationToken ct)
    {
        var b = req.Body;
        var dup = await _db.Products.AnyAsync(p => p.Sku == b.Sku, ct);
        if (dup) return Error.Conflict("product.sku.duplicate", $"SKU '{b.Sku}' already exists");

        var p = new Product
        {
            Sku = b.Sku,
            Name = b.Name,
            Description = b.Description,
            CategoryId = b.CategoryId,
            Type = ProductType.Standard,
            TrackInventory = b.TrackInventory,
            DefaultPriceAmount = b.Price,
            DefaultPriceCurrency = b.Currency,
            TaxCode = b.TaxCode
        };
        _db.Products.Add(p);
        await _db.SaveChangesAsync(ct);

        return new ProductDto(p.Id, p.Sku, p.Name, p.Description, p.CategoryId,
            p.Type.ToString(), p.DefaultPriceAmount, p.DefaultPriceCurrency,
            p.TaxCode, p.TrackInventory, p.IsActive);
    }
}
