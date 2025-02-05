using FunicularSwitch.Generators;

namespace EventSourcing.Commands.Templates;

[ResultType(errorType: typeof(ErrorTypeName))]
public abstract partial class ResultTypeName
{
    public static ResultTypeName<T> NotFound<T>(string message) => Error<T>(ErrorTypeName.NotFound(message));
    public static ResultTypeName<T> Conflict<T>(string message) => Error<T>(ErrorTypeName.Conflict(message));
    public static ResultTypeName<T> Forbidden<T>(string message) =>Error<T>(ErrorTypeName.Forbidden(message));
    public static ResultTypeName<T> Internal<T>(string message) => Error<T>(ErrorTypeName.Internal(message));
    public static ResultTypeName<T> InvalidInput<T>(string message) => Error<T>(ErrorTypeName.InvalidInput(message));
}

public partial class ResultTypeName<T> : IResult<T, ErrorTypeName, ResultTypeName<T>>
{
}