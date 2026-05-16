using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pos.Application.Common.Abstractions;
using Pos.BuildingBlocks;
using Pos.Domain.Identity;
using Pos.Domain.Tenancy;

namespace Pos.Application.Features.Tenants;

public sealed record TenantDto(
    Guid Id, string Slug, string Name, string CountryCode, string DefaultCurrency,
    string TimeZoneId, string Status, DateTimeOffset CreatedAt);

public sealed record CreateTenantRequest(
    string Slug, string Name, string CountryCode, string DefaultCurrency, string TimeZoneId,
    string AdminEmail, string AdminDisplayName, string AdminPassword);

public sealed record ListTenantsQuery : IRequest<IReadOnlyList<TenantDto>>;
public sealed record CreateTenantCommand(CreateTenantRequest Body) : IRequest<Result<TenantDto>>;

public class CreateTenantValidator : AbstractValidator<CreateTenantCommand>
{
    public CreateTenantValidator()
    {
        RuleFor(x => x.Body.Slug).NotEmpty().MaximumLength(64).Matches("^[a-z0-9-]+$");
        RuleFor(x => x.Body.Name).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Body.CountryCode).NotEmpty().Length(2);
        RuleFor(x => x.Body.DefaultCurrency).NotEmpty().Length(3);
        RuleFor(x => x.Body.AdminEmail).NotEmpty().EmailAddress();
        RuleFor(x => x.Body.AdminPassword).NotEmpty().MinimumLength(8);
        RuleFor(x => x.Body.AdminDisplayName).NotEmpty().MaximumLength(128);
    }
}

public class ListTenantsHandler : IRequestHandler<ListTenantsQuery, IReadOnlyList<TenantDto>>
{
    private readonly IAppDbContext _db;
    public ListTenantsHandler(IAppDbContext db) => _db = db;

    public async Task<IReadOnlyList<TenantDto>> Handle(ListTenantsQuery req, CancellationToken ct) =>
        await _db.Tenants.IgnoreQueryFilters().AsNoTracking()
            .Where(t => t.DeletedAt == null)
            .OrderBy(t => t.Name)
            .Select(t => new TenantDto(t.Id, t.Slug, t.Name, t.CountryCode, t.DefaultCurrency,
                t.TimeZoneId, t.Status.ToString(), t.CreatedAt))
            .ToListAsync(ct);
}

public class CreateTenantHandler : IRequestHandler<CreateTenantCommand, Result<TenantDto>>
{
    private readonly IAppDbContext _db;
    private readonly IPasswordHasher _hasher;
    public CreateTenantHandler(IAppDbContext db, IPasswordHasher hasher) { _db = db; _hasher = hasher; }

    public async Task<Result<TenantDto>> Handle(CreateTenantCommand req, CancellationToken ct)
    {
        var b = req.Body;
        var dup = await _db.Tenants.IgnoreQueryFilters().AnyAsync(t => t.Slug == b.Slug, ct);
        if (dup) return Error.Conflict("tenant.slug.duplicate", $"Tenant slug '{b.Slug}' already exists");

        var t = new Tenant
        {
            Slug = b.Slug,
            Name = b.Name,
            CountryCode = b.CountryCode,
            DefaultCurrency = b.DefaultCurrency,
            TimeZoneId = b.TimeZoneId,
            Status = TenantStatus.Active,
            IsolationMode = TenantIsolationMode.Pooled
        };
        _db.Tenants.Add(t);

        var adminRole = new Role
        {
            TenantId = t.Id,
            Name = "Admin",
            Permissions = Permissions.All,
            IsSystem = true
        };
        _db.Roles.Add(adminRole);

        var admin = new User
        {
            TenantId = t.Id,
            Email = b.AdminEmail,
            DisplayName = b.AdminDisplayName,
            PasswordHash = _hasher.Hash(b.AdminPassword),
            IsActive = true
        };
        _db.Users.Add(admin);

        await _db.SaveChangesAsync(ct);

        return new TenantDto(t.Id, t.Slug, t.Name, t.CountryCode, t.DefaultCurrency,
            t.TimeZoneId, t.Status.ToString(), t.CreatedAt);
    }
}
