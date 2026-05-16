using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Pos.Application.Common.Behaviors;

public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;
    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger) => _logger = logger;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var name = typeof(TRequest).Name;
        var sw = Stopwatch.StartNew();
        try
        {
            var resp = await next();
            _logger.LogInformation("{Request} handled in {Ms}ms", name, sw.ElapsedMilliseconds);
            return resp;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Request} failed after {Ms}ms", name, sw.ElapsedMilliseconds);
            throw;
        }
    }
}
