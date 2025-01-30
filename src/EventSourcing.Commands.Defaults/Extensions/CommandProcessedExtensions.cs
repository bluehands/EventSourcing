using System.Reactive;

namespace EventSourcing.Funicular.Commands.Defaults.Extensions;

public static class CommandProcessedExtensions
{
    public static Result<Unit> ToResult(this CommandProcessed<Failure> commandProcessed) =>
        commandProcessed.ToResult<Result<Unit>>();
}