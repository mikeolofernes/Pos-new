using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Pos.Domain.Common;

namespace Pos.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _log;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> log)
    { _next = next; _log = log; }

    public async Task Invoke(HttpContext ctx)
    {
        try { await _next(ctx); }
        catch (ValidationException ex)
        {
            await Write(ctx, 400, new ProblemDetails
            {
                Type = "about:blank",
                Title = "Validation failed",
                Status = 400,
                Detail = string.Join("; ", ex.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}"))
            });
        }
        catch (DomainException ex)
        {
            await Write(ctx, 409, new ProblemDetails { Title = ex.Code, Status = 409, Detail = ex.Message });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Unhandled");
            await Write(ctx, 500, new ProblemDetails { Title = "Internal Server Error", Status = 500, Detail = ex.Message });
        }
    }

    private static async Task Write(HttpContext ctx, int status, ProblemDetails p)
    {
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "application/problem+json";
        await ctx.Response.WriteAsync(JsonSerializer.Serialize(p));
    }
}
