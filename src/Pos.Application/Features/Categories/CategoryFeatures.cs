using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pos.Application.Common.Abstractions;
using Pos.BuildingBlocks;
using Pos.Domain.Catalog;

namespace Pos.Application.Features.Categories;

public sealed record CategoryDto(Guid Id, Guid? ParentId, string Name, string Slug);

public sealed record CreateCategoryRequest(Guid? ParentId, string Name, string Slug);
public sealed record UpdateCategoryRequest(Guid? ParentId, string Name, string Slug);

public sealed record ListCategoriesQuery : IRequest<IReadOnlyList<CategoryDto>>;
public sealed record CreateCategoryCommand(CreateCategoryRequest Body) : IRequest<Result<CategoryDto>>;
public sealed record UpdateCategoryCommand(Guid Id, UpdateCategoryRequest Body) : IRequest<Result<CategoryDto>>;
public sealed record DeleteCategoryCommand(Guid Id) : IRequest<Result<bool>>;

public class CreateCategoryValidator : AbstractValidator<CreateCategoryCommand>
{
    public CreateCategoryValidator()
    {
        RuleFor(x => x.Body.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Body.Slug).NotEmpty().MaximumLength(128).Matches("^[a-z0-9-]+$");
    }
}

public class UpdateCategoryValidator : AbstractValidator<UpdateCategoryCommand>
{
    public UpdateCategoryValidator()
    {
        RuleFor(x => x.Body.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Body.Slug).NotEmpty().MaximumLength(128).Matches("^[a-z0-9-]+$");
    }
}

public class ListCategoriesHandler : IRequestHandler<ListCategoriesQuery, IReadOnlyList<CategoryDto>>
{
    private readonly IAppDbContext _db;
    public ListCategoriesHandler(IAppDbContext db) => _db = db;

    public async Task<IReadOnlyList<CategoryDto>> Handle(ListCategoriesQuery req, CancellationToken ct) =>
        await _db.Categories.AsNoTracking()
            .Where(c => c.DeletedAt == null)
            .OrderBy(c => c.Name)
            .Select(c => new CategoryDto(c.Id, c.ParentId, c.Name, c.Slug))
            .ToListAsync(ct);
}

public class CreateCategoryHandler : IRequestHandler<CreateCategoryCommand, Result<CategoryDto>>
{
    private readonly IAppDbContext _db;
    public CreateCategoryHandler(IAppDbContext db) => _db = db;

    public async Task<Result<CategoryDto>> Handle(CreateCategoryCommand req, CancellationToken ct)
    {
        var b = req.Body;
        var dup = await _db.Categories.AnyAsync(c => c.Slug == b.Slug, ct);
        if (dup) return Error.Conflict("category.slug.duplicate", $"Slug '{b.Slug}' already exists");

        var c = new Category
        {
            ParentId = b.ParentId,
            Name = b.Name,
            Slug = b.Slug
            // TenantId + ShopId populated by AppDbContext.SaveChangesAsync.
        };
        _db.Categories.Add(c);
        await _db.SaveChangesAsync(ct);
        return new CategoryDto(c.Id, c.ParentId, c.Name, c.Slug);
    }
}

public class UpdateCategoryHandler : IRequestHandler<UpdateCategoryCommand, Result<CategoryDto>>
{
    private readonly IAppDbContext _db;
    public UpdateCategoryHandler(IAppDbContext db) => _db = db;

    public async Task<Result<CategoryDto>> Handle(UpdateCategoryCommand req, CancellationToken ct)
    {
        var c = await _db.Categories.FirstOrDefaultAsync(x => x.Id == req.Id, ct);
        if (c is null) return Error.NotFound("category.not_found", "Category not found");
        if (req.Body.ParentId == c.Id)
            return Error.Validation("category.parent.self", "A category cannot be its own parent");

        c.ParentId = req.Body.ParentId;
        c.Name = req.Body.Name;
        c.Slug = req.Body.Slug;
        await _db.SaveChangesAsync(ct);
        return new CategoryDto(c.Id, c.ParentId, c.Name, c.Slug);
    }
}

public class DeleteCategoryHandler : IRequestHandler<DeleteCategoryCommand, Result<bool>>
{
    private readonly IAppDbContext _db;
    public DeleteCategoryHandler(IAppDbContext db) => _db = db;

    public async Task<Result<bool>> Handle(DeleteCategoryCommand req, CancellationToken ct)
    {
        var c = await _db.Categories.FirstOrDefaultAsync(x => x.Id == req.Id, ct);
        if (c is null) return Error.NotFound("category.not_found", "Category not found");

        var inUse = await _db.Products.AnyAsync(p => p.CategoryId == req.Id, ct);
        if (inUse) return Error.Conflict("category.in_use", "Category is assigned to products");

        var hasChildren = await _db.Categories.AnyAsync(x => x.ParentId == req.Id, ct);
        if (hasChildren) return Error.Conflict("category.has_children", "Category has sub-categories");

        c.DeletedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
