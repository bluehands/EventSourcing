using System.Reactive;

namespace EventSourcing.Funicular.Commands;

public static class StreamIds
{
    public static readonly StreamId Command = new("Command", "Command");
}

public static class EventTypes
{
    public const string CommandProcessed = "CommandProcessed";
}

public record CommandProcessed<TFailure>(CommandResult<TFailure> CommandResult)
    : EventPayload(StreamIds.Command, EventTypes.CommandProcessed) where TFailure : notnull
{
    public override string ToString() => $"Processed: {CommandResult}";
    public CommandId CommandId => CommandResult.CommandId;

    //public TResult ToResult<TResult>() where TResult : IResult<Unit, TFailure, TResult> =>
    //    CommandResult
    //        .Match(
    //            processed: p => p.FunctionalResult.Match(
    //                ok: _ => TResult.Ok(Unit.Default),
    //                failed: error => TResult.Error(error.Failure)
    //            ),
    //            unhandled: u => TResult.Error(TFailure.Internal(u.Message)),
    //            faulted: f => TResult.Error(TFailure.Internal(f.ToString())),
    //            cancelled: c => TResult.Error(TFailure.Cancelled(c.ToString()))
    //        );
}