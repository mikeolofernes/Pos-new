using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pos.Application.Common.Abstractions;
using Pos.BuildingBlocks;
using Pos.Domain.Identity;
using Pos.Domain.Tenancy;

namespace Pos.Application.Features.Auth;

public sealed record LoginCommand(string Email, string Password, string? DeviceName)
    : IRequest<Result<LoginResult>>;

public sealed record LoginResult(
    string AccessToken, DateTimeOffset AccessExpiresAt,
    string RefreshToken, DateTimeOffset RefreshExpiresAt,
    Guid TenantId, Guid UserId, string DisplayName, string[] Permissions);

public class LoginValidator : AbstractValidator<LoginCommand>
{
    public LoginValidator()
    {
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
        var email = req.Email.Trim().ToLowerInvariant();

        // Email is globally unique — look up user across all tenants.
        var user = await _db.Users.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == email && u.DeletedAt == null, ct);
        if (user is null || !user.IsActive) return Error.Unauthorized("auth.invalid", "Invalid credentials");
        if (!_hasher.Verify(req.Password, user.PasswordHash))
            return Error.Unauthorized("auth.invalid", "Invalid credentials");

        var tenant = await _db.Tenants.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == user.TenantId && t.DeletedAt == null, ct);
        if (tenant is null) return Error.Unauthorized("auth.invalid", "Invalid credentials");
        if (tenant.Status == TenantStatus.Suspended || tenant.Status == TenantStatus.Cancelled)
            return Error.Forbidden("tenant.disabled", "Tenant is not active");

        // Permissions come from the user's directly-assigned role.
        var role = user.RoleId is { } rid
            ? await _db.Roles.IgnoreQueryFilters().AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == rid, ct)
            : null;
        var perms = role?.Permissions ?? Array.Empty<string>();

        var device = new Device
        {
            TenantId = tenant.Id,
            Name = req.DeviceName ?? "browser",
            Type = "browser",
            LastSeenAt = DateTimeOffset.UtcNow
        };
        _db.Devices.Add(device);

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
