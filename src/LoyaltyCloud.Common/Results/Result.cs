namespace LoyaltyCloud.Common.Results;

/// <summary>
/// Resultado de una operación que puede tener éxito o fallar sin devolver valor.
/// </summary>
/// <remarks>
/// Patrón Result: usado en lugar de excepciones para flujos esperados
/// (validación, no encontrado, regla de negocio violada). Las excepciones
/// quedan reservadas para errores verdaderamente inesperados.
/// </remarks>
public class Result
{
    /// <summary>Indica si la operación completó exitosamente.</summary>
    public bool IsSuccess { get; }

    /// <summary>Indica si la operación falló. Equivalente a <c>!IsSuccess</c>.</summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>Lista de errores en caso de falla. Vacía si <see cref="IsSuccess"/>.</summary>
    public IReadOnlyList<string> Errors { get; }

    /// <summary>Primer mensaje de error, o <c>string.Empty</c> si no hay errores.</summary>
    public string Error => Errors.Count > 0 ? Errors[0] : string.Empty;

    /// <summary>Constructor protegido — usar fábricas <see cref="Ok()"/> / <see cref="Fail(string)"/>.</summary>
    protected Result(bool success, IReadOnlyList<string> errors)
    {
        if (success && errors.Count > 0)
            throw new InvalidOperationException("Un Result exitoso no puede contener errores.");
        if (!success && errors.Count == 0)
            throw new InvalidOperationException("Un Result fallido debe contener al menos un error.");

        IsSuccess = success;
        Errors = errors;
    }

    /// <summary>Crea un resultado exitoso sin valor.</summary>
    public static Result Ok() => new(true, Array.Empty<string>());

    /// <summary>Crea un resultado fallido con un único mensaje de error.</summary>
    public static Result Fail(string error) => new(false, new[] { error });

    /// <summary>Crea un resultado fallido con una colección de errores.</summary>
    public static Result Fail(IEnumerable<string> errors) =>
        new(false, errors.ToList().AsReadOnly());

    /// <summary>Crea un resultado tipado exitoso.</summary>
    public static Result<T> Ok<T>(T value) => Result<T>.Ok(value);

    /// <summary>Crea un resultado tipado fallido con un mensaje.</summary>
    public static Result<T> Fail<T>(string error) => Result<T>.Fail(error);

    /// <summary>Crea un resultado tipado fallido con una colección de errores.</summary>
    public static Result<T> Fail<T>(IEnumerable<string> errors) => Result<T>.Fail(errors);
}

/// <summary>
/// Resultado tipado de una operación. <see cref="Value"/> solo es válido si <see cref="Result.IsSuccess"/>.
/// </summary>
/// <typeparam name="T">Tipo del valor retornado en caso de éxito.</typeparam>
public class Result<T> : Result
{
    private readonly T? _value;

    /// <summary>
    /// Valor retornado por la operación exitosa.
    /// Lanza <see cref="InvalidOperationException"/> si la operación falló.
    /// </summary>
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("No se puede acceder a Value en un Result fallido.");

    private Result(T value) : base(true, Array.Empty<string>())
    {
        _value = value;
    }

    private Result(IReadOnlyList<string> errors) : base(false, errors)
    {
        _value = default;
    }

    /// <summary>Crea un resultado tipado exitoso con el valor dado.</summary>
    public static new Result<T> Ok(T value) => new(value);

    /// <summary>Crea un resultado tipado fallido con un mensaje de error.</summary>
    public static new Result<T> Fail(string error) => new(new[] { error });

    /// <summary>Crea un resultado tipado fallido con una colección de errores.</summary>
    public static new Result<T> Fail(IEnumerable<string> errors) =>
        new(errors.ToList().AsReadOnly());

    /// <summary>
    /// Transforma el valor en caso de éxito. Si la operación falló, propaga los errores.
    /// </summary>
    public Result<TNew> Map<TNew>(Func<T, TNew> mapper) =>
        IsSuccess ? Result<TNew>.Ok(mapper(_value!)) : Result<TNew>.Fail(Errors);

    /// <summary>
    /// Encadena otra operación que también retorna <see cref="Result{TNew}"/>.
    /// Solo se ejecuta si la operación previa fue exitosa.
    /// </summary>
    public Result<TNew> Bind<TNew>(Func<T, Result<TNew>> binder) =>
        IsSuccess ? binder(_value!) : Result<TNew>.Fail(Errors);

    /// <summary>Conversión implícita desde valor — facilita <c>return value;</c> en handlers.</summary>
    public static implicit operator Result<T>(T value) => Ok(value);
}
