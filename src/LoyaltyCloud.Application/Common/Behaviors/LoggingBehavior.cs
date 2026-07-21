using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace LoyaltyCloud.Application.Common.Behaviors;

/// <summary>
/// Pipeline behavior que loguea inicio/fin de cada request con duración.
/// Loguea solo el tipo del request — nunca el body — para evitar fugar PII
/// (email, teléfono, fecha de nacimiento) a los logs.
/// </summary>
public sealed class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var sw = Stopwatch.StartNew();

        _logger.LogInformation("➡ {Request} starting", requestName);

        try
        {
            var response = await next();
            sw.Stop();
            _logger.LogInformation("✓ {Request} completed in {ElapsedMs} ms", requestName, sw.ElapsedMilliseconds);
            return response;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "✗ {Request} failed after {ElapsedMs} ms", requestName, sw.ElapsedMilliseconds);
            throw;
        }
    }
}
