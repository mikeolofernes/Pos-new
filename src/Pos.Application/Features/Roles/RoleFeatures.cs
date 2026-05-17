using MediatR;
using Microsoft.EntityFrameworkCore;
using Pos.Application.Common.Abstractions;

namespace Pos.Application.Features.Roles;

public sealed record RoleDto(Guid Id, string Name, IReadOnlyList<string> Permissions, bool IsSystem);

public sealed record ListRolesQuery : IRequest<IReadOnlyList<RoleDto>>;

public class ListRolesHandler : IRequestHandler<ListRolesQuery, IReadOnlyList<RoleDto>>
{
    private readonly IAppDbContext _db;
    public ListRolesHandler(IAppDbContext db) => _db = db;

    public async Task<IReadOnlyList<RoleDto>> Handle(ListRolesQuery req, CancellationToken ct) =>
        await _db.Roles.AsNoTracking()
            .Where(r => r.DeletedAt == null)
            .OrderBy(r => r.Name)
            .Select(r => new RoleDto(r.Id, r.Name, r.Permissions.ToList(), r.IsSystem))
            .ToListAsync(ct);
}
