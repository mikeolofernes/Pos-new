using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pos.Application.Common.Abstractions;
using Pos.BuildingBlocks;
using Pos.Domain.Identity;
using Pos.Domain.Tenancy;

namespace Pos.Application.Features.Auth;

public sealed record LoginCommand(string TenantSlug, string Email, string Password, string? DeviceName)
    : IRequest<Result<LoginResult>>;

public sealed record LoginResult(
    string AccessToken, DateTimeOffset AccessExpiresAt,
    string RefreshToken, DateTimeOffset RefreshExpiresAt,
    Guid TenantId, Guid UserId, string DisplayName, string[] Permissions);

public class LoginValidator : AbstractValidator<LoginCommand>
{
    public LoginValidator()
    {
        RuleFor(x => x.TenantSlug).NotEmpty();
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(6);
    }
}

public class LoginHandler : IRequestHandler<LoginCommand, Result<LoginResult>>
{
    private readonly IAppDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly IJwtService _jwt;

    public LoginHandler(IAppDbContext db, IPasswordHasher hasher, IJwtService jwt)
    { _db = db; _hasher = hasher; _jwt = jwt; }

    public async Task<Result<LoginResult>> Handle(LoginCommand req, CancellationToken ct)
    {
        var tenant = await _db.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Slug == req.TenantSlug && t.DeletedAt == null, ct);
        if (tenant is null) return Error.Unauthorized("auth.invalid", "Invalid credentials");
        if (tenant.Status == TenantStatus.Suspended || tenant.Status == TenantStatus.Cancelled)
            return Error.Forbidden("tenant.disabled", "Tenant is not active");

        var email = req.Email.Trim().ToLowerInvariant();
        // Bypass query filter: we don't have a tenant context yet at login time.
        var user = await _db.Users.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(u => u.TenantId == tenant.Id && u.Email.ToLower() == email && u.DeletedAt == null, ct);
        if (user is null || !user.IsActive) return Error.Unauthorized("auth.invalid", "Invalid credentials");
        if (!_hasher.Verify(req.Password, user.PasswordHash))
            return Error.Unauthorized("auth.invalid", "Invalid credentials");

        // Repair any UserShopRole rows that were saved with TenantId = Guid.Empty (pre-fix data).
        var staleRoles = await _db.UserShopRoles.IgnoreQueryFilters()
            .Where(r => r.UserId == user.Id && r.TenantId == Guid.Empty)
            .ToListAsync(ct);
        if (staleRoles.Count > 0)
        {
            foreach (var sr in staleRoles) sr.TenantId = tenant.Id;
            await _db.SaveChangesAsync(ct);
        }

        var permissions = await _db.UserShopRoles.IgnoreQueryFilters().AsNoTracking()
            .Where(usr => usr.TenantId == tenant.Id && usr.UserId == user.Id)
            .Join(_db.Roles.IgnoreQueryFilters(), usr => usr.RoleId, r => r.Id, (usr, r) => r.Permissions)
            .ToListAsync(ct);

        var perms = permissions.SelectMany(p => p).Distinct().ToArray();

        var device = new Device
        {
            TenantId = tenant.Id,
            Name = req.DeviceName ?? "browser",
            Type = "browser",
            LastSeenAt = DateTimeOffset.UtcNow
        };
        _db.Devices.Add(device);
        // Bypass tenant filter during login by detaching change-tracker reliance on _tenant
        // (TenantId is set explicitly above).

        var tokens = _jwt.Issue(tenant.Id, user.Id, user.Email, perms, device.Id);

        _db.RefreshTokens.Add(new RefreshToken
        {
            TenantId = tenant.Id,
            UserId = user.Id,
            DeviceId = device.Id,
            FamilyId = UlidGuid.NewUlidGuid(),
            TokenHash = _jwt.HashRefresh(tokens.RefreshToken),
            ExpiresAt = tokens.RefreshExpiresAt
        });

        user.LastLoginAt = DateTimeOffset.UtcNow;
        _db.Users.Attach(user).Property(u => u.LastLoginAt).IsModified = true;

        await _db.SaveChangesAsync(ct);

        return new LoginResult(tokens.AccessToken, tokens.AccessExpiresAt,
            tokens.RefreshToken, tokens.RefreshExpiresAt,
            tenant.Id, user.Id, user.DisplayName, perms);
    }
}
