using FunicularSwitch.Generators;

namespace EventSourcing.Funicular.Commands.Defaults;

[ResultType(errorType: typeof(Failure))]
public abstract partial class OperationResult
{
}

public partial class OperationResult<T> : IResult<T, Failure, OperationResult<T>>
{
}