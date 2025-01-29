using FunicularSwitch.Generators;

namespace EventSourcing.Funicular.Commands.Defaults;

[ResultType(errorType: typeof(Failure))]
public abstract partial class Result
{
}

public partial class Result<T> : IResult<T, Failure, Result<T>>
{
}