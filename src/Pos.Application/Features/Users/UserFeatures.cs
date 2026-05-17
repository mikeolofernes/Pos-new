using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pos.Application.Common.Abstractions;
using Pos.BuildingBlocks;
using Pos.Domain.Identity;

namespace Pos.Application.Features.Users;

public sealed record UserDto(
    Guid Id, string Email, string DisplayName, string? Phone, bool IsActive,
    DateTimeOffset? LastLoginAt, DateTimeOffset CreatedAt, IReadOnlyList<Guid> ShopIds);

public sealed record CreateUserRequest(
    string Email, string DisplayName, string? Phone, string Password, bool IsActive,
    IReadOnlyList<Guid>? ShopIds);

public sealed record UpdateUserRequest(
    string DisplayName, string? Phone, bool IsActive, string? NewPassword,
    IReadOnlyList<Guid>? ShopIds);

public sealed record ListUsersQuery(string? Q) : IRequest<IReadOnlyList<UserDto>>;
public sealed record CreateUserCommand(CreateUserRequest Body) : IRequest<Result<UserDto>>;
public sealed record UpdateUserCommand(Guid Id, UpdateUserRequest Body) : IRequest<Result<UserDto>>;
public sealed record DeleteUserCommand(Guid Id) : IRequest<Result<bool>>;

public class CreateUserValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserValidator()
    {
        RuleFor(x => x.Body.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Body.DisplayName).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Body.Password).NotEmpty().MinimumLength(8);
    }
}

public class UpdateUserValidator : AbstractValidator<UpdateUserCommand>
{
    public UpdateUserValidator()
    {
        RuleFor(x => x.Body.DisplayName).NotEmpty().MaximumLength(128);
        When(x => !string.IsNullOrEmpty(x.Body.NewPassword), () =>
            RuleFor(x => x.Body.NewPassword!).MinimumLength(8));
    }
}

internal static class UserShopRoleSync
{
    /// <summary>Sync UserShopRole rows for a user: add missing, soft-delete removed.</summary>
    public static async Task SyncAsync(
        IAppDbContext db, Guid userId, IReadOnlyList<Guid> wantedShopIds, CancellationToken ct)
    {
        // Default role for newly assigned shops: prefer "Cashier", fall back to "Admin".
        var defaultRole = await db.Roles.AsNoTracking()
            .Where(r => r.DeletedAt == null && (r.Name == "Cashier" || r.Name == "Admin"))
            .OrderBy(r => r.Name == "Cashier" ? 0 : 1)
            .FirstOrDefaultAsync(ct);
        if (defaultRole is null) return; // no roles in tenant yet — skip silently

        var existing = await db.UserShopRoles
            .Where(r => r.UserId == userId && r.DeletedAt == null)
            .ToListAsync(ct);

        var existingShopIds = existing.Select(r => r.ShopId).ToHashSet();
        var wantedSet = wantedShopIds.ToHashSet();

        foreach (var stale in existing.Where(r => !wantedSet.Contains(r.ShopId)))
            stale.DeletedAt = DateTimeOffset.UtcNow;

        foreach (var shopId in wantedSet.Where(s => !existingShopIds.Contains(s)))
        {
            db.UserShopRoles.Add(new UserShopRole
            {
                UserId = userId,
                ShopId = shopId,
                RoleId = defaultRole.Id
            });
        }
    }

    public static async Task<IReadOnlyList<Guid>> GetShopIdsAsync(
        IAppDbContext db, Guid userId, CancellationToken ct) =>
        await db.UserShopRoles.AsNoTracking()
            .Where(r => r.UserId == userId)
            .Select(r => r.ShopId).Distinct().ToListAsync(ct);
}

public class ListUsersHandler : IRequestHandler<ListUsersQuery, IReadOnlyList<UserDto>>
{
    private readonly IAppDbContext _db;
    public ListUsersHandler(IAppDbContext db) => _db = db;

    public async Task<IReadOnlyList<UserDto>> Handle(ListUsersQuery req, CancellationToken ct)
    {
        var q = _db.Users.AsNoTracking().Where(u => u.DeletedAt == null);
        if (!string.IsNullOrWhiteSpace(req.Q))
        {
            var s = req.Q.Trim();
            q = q.Where(u => u.Email.Contains(s) || u.DisplayName.Contains(s));
        }
        var users = await q.OrderBy(u => u.DisplayName).ToListAsync(ct);
        var userIds = users.Select(u => u.Id).ToList();
        var pairs = await _db.UserShopRoles.AsNoTracking()
            .Where(r => userIds.Contains(r.UserId))
            .Select(r => new { r.UserId, r.ShopId })
            .ToListAsync(ct);
        var byUser = pairs.GroupBy(p => p.UserId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<Guid>)g.Select(x => x.ShopId).Distinct().ToList());
        return users.Select(u => new UserDto(
            u.Id, u.Email, u.DisplayName, u.Phone, u.IsActive, u.LastLoginAt, u.CreatedAt,
            byUser.TryGetValue(u.Id, out var ids) ? ids : Array.Empty<Guid>())).ToList();
    }
}

public class CreateUserHandler : IRequestHandler<CreateUserCommand, Result<UserDto>>
{
    private readonly IAppDbContext _db;
    private readonly IPasswordHasher _hasher;
    public CreateUserHandler(IAppDbContext db, IPasswordHasher hasher) { _db = db; _hasher = hasher; }

    public async Task<Result<UserDto>> Handle(CreateUserCommand req, CancellationToken ct)
    {
        var b = req.Body;
        var dup = await _db.Users.AnyAsync(u => u.Email == b.Email, ct);
        if (dup) return Error.Conflict("user.email.duplicate", $"Email '{b.Email}' already exists");

        var u = new User
        {
            Email = b.Email,
            DisplayName = b.DisplayName,
            Phone = b.Phone,
            PasswordHash = _hasher.Hash(b.Password),
            IsActive = b.IsActive
        };
        _db.Users.Add(u);
        await _db.SaveChangesAsync(ct);

        if (b.ShopIds is { Count: > 0 })
        {
            await UserShopRoleSync.SyncAsync(_db, u.Id, b.ShopIds, ct);
            await _db.SaveChangesAsync(ct);
        }

        var ids = await UserShopRoleSync.GetShopIdsAsync(_db, u.Id, ct);
        return new UserDto(u.Id, u.Email, u.DisplayName, u.Phone, u.IsActive, u.LastLoginAt, u.CreatedAt, ids);
    }
}

public class UpdateUserHandler : IRequestHandler<UpdateUserCommand, Result<UserDto>>
{
    private readonly IAppDbContext _db;
    private readonly IPasswordHasher _hasher;
    public UpdateUserHandler(IAppDbContext db, IPasswordHasher hasher) { _db = db; _hasher = hasher; }

    public async Task<Result<UserDto>> Handle(UpdateUserCommand req, CancellationToken ct)
    {
        var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == req.Id, ct);
        if (u is null) return Error.NotFound("user.not_found", "User not found");
        var b = req.Body;
        u.DisplayName = b.DisplayName;
        u.Phone = b.Phone;
        u.IsActive = b.IsActive;
        if (!string.IsNullOrEmpty(b.NewPassword)) u.PasswordHash = _hasher.Hash(b.NewPassword);

        if (b.ShopIds is not null)
            await UserShopRoleSync.SyncAsync(_db, u.Id, b.ShopIds, ct);

        await _db.SaveChangesAsync(ct);

        var ids = await UserShopRoleSync.GetShopIdsAsync(_db, u.Id, ct);
        return new UserDto(u.Id, u.Email, u.DisplayName, u.Phone, u.IsActive, u.LastLoginAt, u.CreatedAt, ids);
    }
}

public class DeleteUserHandler : IRequestHandler<DeleteUserCommand, Result<bool>>
{
    private readonly IAppDbContext _db;
    public DeleteUserHandler(IAppDbContext db) => _db = db;

    public async Task<Result<bool>> Handle(DeleteUserCommand req, CancellationToken ct)
    {
        var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == req.Id, ct);
        if (u is null) return Error.NotFound("user.not_found", "User not found");
        u.DeletedAt = DateTimeOffset.UtcNow;
        u.IsActive = false;
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
