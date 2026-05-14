using FluentValidation;
using KBeauty.Loyalty.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace KBeauty.Loyalty.API.Middleware;

/// <summary>
/// Captura excepciones no manejadas y las traduce a <see cref="ProblemDetails"/>
/// RFC 7807 con un código estable para el cliente. Los flujos esperados ya van
/// vía <c>Result&lt;T&gt;</c> — esto es solo el "último recurso".
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, code, detail) = exception switch
        {
            // Excepciones de dominio: el cliente puede actuar sobre ellas → 422.
            InsufficientPointsException ipe =>
                (StatusCodes.Status422UnprocessableEntity, ipe.Code, ipe.Message),
            LevelNotEligibleException lne =>
                (StatusCodes.Status422UnprocessableEntity, lne.Code, lne.Message),
            CustomerNotFoundException cnf =>
                (StatusCodes.Status404NotFound, cnf.Code, cnf.Message),
            CardAlreadyExistsException cae =>
                (StatusCodes.Status409Conflict, cae.Code, cae.Message),
            RedemptionAlreadyConfirmedException rac =>
                (StatusCodes.Status409Conflict, rac.Code, rac.Message),
            DomainException dex =>
                (StatusCodes.Status422UnprocessableEntity, dex.Code, dex.Message),

            // FluentValidation: solo llega aquí si TResponse no es Result (raro en nuestro stack).
            ValidationException ve =>
                (StatusCodes.Status400BadRequest, "VALIDATION_FAILED", ve.Message),

            // Cualquier otra: 500 genérico — no exponemos el mensaje interno.
            _ => (StatusCodes.Status500InternalServerError, "INTERNAL_ERROR",
                "Ocurrió un error inesperado.")
        };

        // Loguea con stack solo para 5xx; las excepciones de dominio son ruido en logs.
        if (status >= 500)
            _logger.LogError(exception, "Unhandled {Code} at {Path}", code, httpContext.Request.Path);
        else
            _logger.LogInformation("Handled domain exception {Code}: {Message}", code, exception.Message);

        var problem = new ProblemDetails
        {
            Status = status,
            Type = $"https://kbeauty.mx/errors/{code.ToLowerInvariant()}",
            Title = code,
            Detail = detail,
            Instance = httpContext.Request.Path
        };

        httpContext.Response.StatusCode = status;
        httpContext.Response.ContentType = "application/problem+json";
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }
}
