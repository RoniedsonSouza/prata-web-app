namespace Prata.Domain.Common;

/// <summary>
/// Resultado de operação de domínio. Regra violada é valor, não exceção
/// (docs/02-ARQUITETURA.md §4.1).
/// </summary>
public class Result
{
    protected Result(bool isSuccess, Error? error)
    {
        if (isSuccess && error is not null)
            throw new ArgumentException("Resultado de sucesso nao pode carregar erro.");
        if (!isSuccess && error is null)
            throw new ArgumentException("Resultado de falha precisa de erro.");

        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public Error? Error { get; }

    public static Result Success() => new(true, null);

    public static Result Failure(Error error) => new(false, error);

    public static Result<T> Success<T>(T value) => Result<T>.Success(value);

    public static Result<T> Failure<T>(Error error) => Result<T>.Failure(error);

    public static implicit operator Result(Error error) => Failure(error);
}

/// <summary>
/// Resultado tipado. <see cref="Value"/> so e seguro apos <see cref="Result.IsSuccess"/>.
/// </summary>
public sealed class Result<T> : Result
{
    private readonly T? _value;

    private Result(T value)
        : base(true, null)
    {
        _value = value;
    }

    private Result(Error error)
        : base(false, error)
    {
        _value = default;
    }

    public T Value =>
        IsSuccess ? _value! : throw new InvalidOperationException($"Nao e possivel ler Value de um Result falho ({Error!.Value.Code}).");

    public static Result<T> Success(T value) => new(value);

    public static new Result<T> Failure(Error error) => new(error);

    public static implicit operator Result<T>(T value) => Success(value);

    public static implicit operator Result<T>(Error error) => Failure(error);
}
