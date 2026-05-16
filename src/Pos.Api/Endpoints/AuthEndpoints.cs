using MediatR;
using Pos.Api.Common;
using Pos.Application.Features.Auth;

namespace Pos.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/v1/auth").WithTags("Auth");

        g.MapPost("/login", async (LoginCommand cmd, ISender sender, CancellationToken ct) =>
            (await sender.Send(cmd, ct)).ToHttp())
         .AllowAnonymous();

        g.MapPost("/refresh", async (RefreshCommand cmd, ISender sender, CancellationToken ct) =>
            (await sender.Send(cmd, ct)).ToHttp())
         .AllowAnonymous();

        return app;
    }
}
