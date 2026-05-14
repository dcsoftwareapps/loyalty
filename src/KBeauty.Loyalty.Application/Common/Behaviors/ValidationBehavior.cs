using System.Reflection;
using FluentValidation;
using KBeauty.Loyalty.Common.Results;
using MediatR;

namespace KBeauty.Loyalty.Application.Common.Behaviors;

/// <summary>
/// Pipeline behavior que corre todos los <see cref="IValidator{TRequest}"/>
/// asociados al request antes de invocar el handler. Si falla la validación
/// y el response es <c>Result</c> o <c>Result&lt;T&gt;</c>, retorna un fallo
/// tipado en vez de lanzar excepción (alineado con la política "no excepciones
/// para flujos esperados").
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    // Cache estático por par (TRequest, TResponse): construir el factory de Fail
    // por reflexión solo una vez.
    private static readonly Func<IEnumerable<string>, TResponse>? _failFactory = TryBuildFailFactory();

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    /// <inheritdoc />
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!_validators.Any()) return await next();

        var context = new ValidationContext<TRequest>(request);
        var results = await Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        var failures = results
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .Select(f => f.ErrorMessage)
            .ToList();

        if (failures.Count == 0) return await next();

        // Si TResponse es Result o Result<T>, devolvemos fallo tipado.
        if (_failFactory is not null)
            return _failFactory(failures);

        // Tipo de respuesta no compatible con Result — esto no debería pasar en
        // nuestros handlers; si pasa, lanzamos para no enmascarar el error.
        throw new ValidationException(string.Join("; ", failures));
    }

    private static Func<IEnumerable<string>, TResponse>? TryBuildFailFactory()
    {
        var responseType = typeof(TResponse);

        // Caso 1: TResponse == Result (no genérico) → usa Result.Fail(IEnumerable<string>)
        // Caso 2: TResponse == Result<T> → usa Result<T>.Fail(IEnumerable<string>) (declarada con `new`)
        var method = responseType
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(m =>
                m.Name == nameof(Result.Fail) &&
                !m.IsGenericMethod &&
                m.ReturnType == responseType &&
                m.GetParameters() is { Length: 1 } p &&
                p[0].ParameterType == typeof(IEnumerable<string>));

        if (method is null) return null;

        return errors => (TResponse)method.Invoke(null, new object[] { errors })!;
    }
}
