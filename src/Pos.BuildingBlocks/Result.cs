namespace Pos.BuildingBlocks;

public readonly record struct Error(string Code, string Message, ErrorType Type = ErrorType.Failure)
{
    public static readonly Error None = new(string.Empty, string.Empty);
    public static Error NotFound(string code, string msg) => new(code, msg, ErrorType.NotFound);
    public static Error Validation(string code, string msg) => new(code, msg, ErrorType.Validation);
    public static Error Conflict(string code, string msg) => new(code, msg, ErrorType.Conflict);
    public static Error Unauthorized(string code, string msg) => new(code, msg, ErrorType.Unauthorized);
    public static Error Forbidden(string code, string msg) => new(code, msg, ErrorType.Forbidden);
}

public enum ErrorType { Failure, Validation, NotFound, Conflict, Unauthorized, Forbidden }

public readonly struct Result<T>
{
    public T? Value { get; }
    public Error Error { get; }
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;

    private Result(T value) { Value = value; Error = Error.None; IsSuccess = true; }
    private Result(Error error) { Value = default; Error = error; IsSuccess = false; }

    public static Result<T> Success(T value) => new(value);
    public static Result<T> Failure(Error error) => new(error);

    public static implicit operator Result<T>(T value) => Success(value);
    public static implicit operator Result<T>(Error error) => Failure(error);

    public TR Match<TR>(Func<T, TR> ok, Func<Error, TR> fail) => IsSuccess ? ok(Value!) : fail(Error);
}

public readonly struct Result
{
    public Error Error { get; }
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;

    private Result(bool ok, Error error) { IsSuccess = ok; Error = error; }
    public static Result Success() => new(true, Error.None);
    public static Result Failure(Error error) => new(false, error);
    public static implicit operator Result(Error error) => Failure(error);
}
