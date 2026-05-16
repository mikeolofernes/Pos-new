using MediatR;
using Microsoft.EntityFrameworkCore;
using Pos.Application.Common.Abstractions;
using Pos.BuildingBlocks;
using Pos.Domain.Identity;

namespace Pos.Application.Features.Auth;

public sealed record RefreshCommand(string RefreshToken) : IRequest<Result<LoginResult>>;

public class RefreshHandler : IRequestHandler<RefreshCommand, Result<LoginResult>>
{
    private readonly IAppDbContext _db;
    private readonly IJwtService _jwt;

    public RefreshHandler(IAppDbContext db, IJwtService jwt) { _db = db; _jwt = jwt; }

    public async Task<Result<LoginResult>> Handle(RefreshCommand req, CancellationToken ct)
    {
        var hash = _jwt.HashRefresh(req.RefreshToken);
        var existing = await _db.RefreshTokens.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.TokenHash == hash && t.DeletedAt == null, ct);

        if (existing is null) return Error.Unauthorized("auth.refresh.invalid", "Invalid refresh token");
        if (existing.ExpiresAt <= DateTimeOffset.UtcNow) return Error.Unauthorized("auth.refresh.expired", "Refresh token expired");

        if (existing.RevokedAt is not null)
        {
            // token reuse — revoke whole family
            await _db.RefreshTokens.IgnoreQueryFilters()
                .Where(t => t.FamilyId == existing.FamilyId)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, DateTimeOffset.UtcNow), ct);
            return Error.Unauthorized("auth.refresh.reuse", "Refresh token reuse detected");
        }

        var user = await _db.Users.IgnoreQueryFilters().AsNoTracking()
            .FirstAsync(u => u.Id == existing.UserId, ct);

        var perms = await _db.UserShopRoles.IgnoreQueryFilters().AsNoTracking()
            .Where(usr => usr.UserId == user.Id)
            .Join(_db.Roles.IgnoreQueryFilters(), usr => usr.RoleId, r => r.Id, (usr, r) => r.Permissions)
            .ToListAsync(ct);
        var flatPerms = perms.SelectMany(p => p).Distinct().ToArray();

        var tokens = _jwt.Issue(existing.TenantId, user.Id, user.Email, flatPerms, existing.DeviceId);
        var newHash = _jwt.HashRefresh(tokens.RefreshToken);

        existing.RevokedAt = DateTimeOffset.UtcNow;
        existing.ReplacedByHash = newHash;

        _db.RefreshTokens.Add(new RefreshToken
        {
            TenantId = existing.TenantId,
            UserId = existing.UserId,
            DeviceId = existing.DeviceId,
            FamilyId = existing.FamilyId,
            TokenHash = newHash,
            ExpiresAt = tokens.RefreshExpiresAt
        });

        await _db.SaveChangesAsync(ct);

        return new LoginResult(tokens.AccessToken, tokens.AccessExpiresAt,
            tokens.RefreshToken, tokens.RefreshExpiresAt,
            existing.TenantId, user.Id, user.DisplayName, flatPerms);
    }
}
