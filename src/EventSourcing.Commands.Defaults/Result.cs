using FunicularSwitch.Generators;

namespace EventSourcing.Funicular.Commands.Defaults;

[ResultType(errorType: typeof(Failure))]
public abstract partial class Result
{
    public static Result<T> NotFound<T>(string message) => Error<T>(Failure.NotFound(message));
    public static Result<T> Conflict<T>(string message) => Error<T>(Failure.Conflict(message));
    public static Result<T> Forbidden<T>(string message) => Error<T>(Failure.Forbidden(message));
    public static Result<T> Internal<T>(string message) => Error<T>(Failure.Internal(message));
    public static Result<T> InvalidInput<T>(string message) => Error<T>(Failure.InvalidInput(message));
}

public partial class Result<T> : IResult<T, Failure, Result<T>>
{
}