using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pos.Application.Common.Abstractions;
using Pos.BuildingBlocks;
using Pos.Domain.Shops;

namespace Pos.Application.Features.Shops;

public sealed record CreateShopRequest(string Code, string Name, string CountryCode, Guid? TenantId = null);
public sealed record UpdateShopRequest(string Code, string Name, string CountryCode, bool IsActive);

public sealed record CreateShopCommand(CreateShopRequest Body) : IRequest<Result<ShopDto>>;
public sealed record UpdateShopCommand(Guid Id, UpdateShopRequest Body) : IRequest<Result<ShopDto>>;
public sealed record DeleteShopCommand(Guid Id) : IRequest<Result<bool>>;

public class CreateShopValidator : AbstractValidator<CreateShopCommand>
{
    public CreateShopValidator()
    {
        RuleFor(x => x.Body.Code).NotEmpty().MaximumLength(32);
        RuleFor(x => x.Body.Name).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Body.CountryCode).NotEmpty().Length(2);
    }
}

public class UpdateShopValidator : AbstractValidator<UpdateShopCommand>
{
    public UpdateShopValidator()
    {
        RuleFor(x => x.Body.Code).NotEmpty().MaximumLength(32);
        RuleFor(x => x.Body.Name).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Body.CountryCode).NotEmpty().Length(2);
    }
}

public class CreateShopHandler : IRequestHandler<CreateShopCommand, Result<ShopDto>>
{
    private readonly IAppDbContext _db;
    public CreateShopHandler(IAppDbContext db) => _db = db;

    public async Task<Result<ShopDto>> Handle(CreateShopCommand req, CancellationToken ct)
    {
        var b = req.Body;
        // When the caller specifies a tenant (super-admin path), check duplicates
        // across that tenant; otherwise the query filter scopes to current tenant.
        var dup = b.TenantId is { } overrideTid
            ? await _db.Shops.IgnoreQueryFilters().AnyAsync(s => s.Code == b.Code && s.TenantId == overrideTid && s.DeletedAt == null, ct)
            : await _db.Shops.AnyAsync(s => s.Code == b.Code, ct);
        if (dup) return Error.Conflict("shop.code.duplicate", $"Shop code '{b.Code}' already exists");

        var s = new Shop
        {
            Code = b.Code,
            Name = b.Name,
            CountryCode = b.CountryCode,
            IsActive = true
            // TenantId is auto-populated from request context by SaveChangesAsync
            // unless the caller passed an explicit one (super-admin path).
        };
        if (b.TenantId is { } tid) s.TenantId = tid;
        _db.Shops.Add(s);
        await _db.SaveChangesAsync(ct);
        return new ShopDto(s.Id, s.Code, s.Name, s.IsActive);
    }
}

public class UpdateShopHandler : IRequestHandler<UpdateShopCommand, Result<ShopDto>>
{
    private readonly IAppDbContext _db;
    public UpdateShopHandler(IAppDbContext db) => _db = db;

    public async Task<Result<ShopDto>> Handle(UpdateShopCommand req, CancellationToken ct)
    {
        var s = await _db.Shops.FirstOrDefaultAsync(x => x.Id == req.Id, ct);
        if (s is null) return Error.NotFound("shop.not_found", "Shop not found");
        var b = req.Body;
        s.Code = b.Code;
        s.Name = b.Name;
        s.CountryCode = b.CountryCode;
        s.IsActive = b.IsActive;
        await _db.SaveChangesAsync(ct);
        return new ShopDto(s.Id, s.Code, s.Name, s.IsActive);
    }
}

public class DeleteShopHandler : IRequestHandler<DeleteShopCommand, Result<bool>>
{
    private readonly IAppDbContext _db;
    public DeleteShopHandler(IAppDbContext db) => _db = db;

    public async Task<Result<bool>> Handle(DeleteShopCommand req, CancellationToken ct)
    {
        var s = await _db.Shops.FirstOrDefaultAsync(x => x.Id == req.Id, ct);
        if (s is null) return Error.NotFound("shop.not_found", "Shop not found");
        s.DeletedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
