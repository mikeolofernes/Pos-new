using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Pos.BuildingBlocks;

namespace Pos.Api.Common;

public static class ResultExtensions
{
    public static IResult ToHttp<T>(this Result<T> r, Func<T, IResult>? onSuccess = null) =>
        r.IsSuccess
            ? (onSuccess?.Invoke(r.Value!) ?? Results.Ok(r.Value))
            : ToProblem(r.Error);

    public static IResult ToProblem(this Error e) =>
        Results.Problem(new ProblemDetails
        {
            Title = e.Code,
            Detail = e.Message,
            Status = e.Type switch
            {
                ErrorType.Validation => StatusCodes.Status400BadRequest,
                ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
                ErrorType.Forbidden => StatusCodes.Status403Forbidden,
                ErrorType.NotFound => StatusCodes.Status404NotFound,
                ErrorType.Conflict => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status400BadRequest
            }
        });
}
