using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pos.Application.Common.Abstractions;
using Pos.BuildingBlocks;
using Pos.Domain.Identity;

namespace Pos.Application.Features.Users;

public sealed record UserDto(
    Guid Id, string Email, string DisplayName, string? Phone, bool IsActive,
    DateTimeOffset? LastLoginAt, DateTimeOffset CreatedAt);

public sealed record CreateUserRequest(string Email, string DisplayName, string? Phone, string Password, bool IsActive);
public sealed record UpdateUserRequest(string DisplayName, string? Phone, bool IsActive, string? NewPassword);

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
        return await q.OrderBy(u => u.DisplayName)
            .Select(u => new UserDto(u.Id, u.Email, u.DisplayName, u.Phone, u.IsActive, u.LastLoginAt, u.CreatedAt))
            .ToListAsync(ct);
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
        return new UserDto(u.Id, u.Email, u.DisplayName, u.Phone, u.IsActive, u.LastLoginAt, u.CreatedAt);
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
        await _db.SaveChangesAsync(ct);
        return new UserDto(u.Id, u.Email, u.DisplayName, u.Phone, u.IsActive, u.LastLoginAt, u.CreatedAt);
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
