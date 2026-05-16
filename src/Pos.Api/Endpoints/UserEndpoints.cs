using MediatR;
using Pos.Api.Common;
using Pos.Application.Features.Users;
using Pos.Domain.Identity;

namespace Pos.Api.Endpoints;

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/v1/users").WithTags("Users").RequireAuthorization();

        g.MapGet("/", async (string? q, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new ListUsersQuery(q), ct)))
         .RequireAuthorization(Permissions.UsersManage);

        g.MapPost("/", async (CreateUserRequest body, ISender sender, CancellationToken ct) =>
                (await sender.Send(new CreateUserCommand(body), ct))
                    .ToHttp(u => Results.Created($"/api/v1/users/{u.Id}", u)))
         .RequireAuthorization(Permissions.UsersManage);

        g.MapPut("/{id:guid}", async (Guid id, UpdateUserRequest body, ISender sender, CancellationToken ct) =>
                (await sender.Send(new UpdateUserCommand(id, body), ct)).ToHttp())
         .RequireAuthorization(Permissions.UsersManage);

        g.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new DeleteUserCommand(id), ct)).ToHttp())
         .RequireAuthorization(Permissions.UsersManage);

        return app;
    }
}
