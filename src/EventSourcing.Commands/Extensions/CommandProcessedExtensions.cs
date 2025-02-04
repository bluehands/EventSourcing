using System.Reactive;

namespace EventSourcing.Funicular.Commands.Extensions;

public static class CommandProcessedExtensions
{
    public static TResult ToResult<TResult, TFailure>(this CommandProcessed<TFailure> commandProcessed) 
        where TResult : IResult<Unit, TFailure, TResult>
        where TFailure : IFailure<TFailure> =>
        commandProcessed.CommandResult
            .Match(
                processed: p => p.FunctionalResult.Match(
                    ok: _ => TResult.Ok(Unit.Default),
                    failed: error => TResult.Error(error.Failure)
                ),
                unhandled: u => TResult.Error(TFailure.Internal(u.Message)),
                faulted: f => TResult.Error(TFailure.Internal(f.ToString())),
                cancelled: c => TResult.Error(TFailure.Cancelled(c.ToString()))
            );
}