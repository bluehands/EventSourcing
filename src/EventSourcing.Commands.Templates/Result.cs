using FunicularSwitch.Generators;

namespace EventSourcing.Commands.Templates;

[ResultType(errorType: typeof(FailureTypeName))]
public abstract partial class ResultTypeName
{
    public static ResultTypeName<T> NotFound<T>(string message) => Error<T>(FailureTypeName.NotFound(message));
    public static ResultTypeName<T> Conflict<T>(string message) => Error<T>(FailureTypeName.Conflict(message));
    public static ResultTypeName<T> Forbidden<T>(string message) =>Error<T>(FailureTypeName.Forbidden(message));
    public static ResultTypeName<T> Internal<T>(string message) => Error<T>(FailureTypeName.Internal(message));
    public static ResultTypeName<T> InvalidInput<T>(string message) => Error<T>(FailureTypeName.InvalidInput(message));
}

public partial class ResultTypeName<T> : IResult<T, FailureTypeName, ResultTypeName<T>>
{
}