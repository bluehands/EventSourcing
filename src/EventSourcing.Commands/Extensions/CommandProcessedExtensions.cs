using System.Reactive;

namespace EventSourcing.Commands.Extensions;

public static class CommandProcessedExtensions
{
    public static TResult ToResult<TResult, TError>(this CommandProcessed<TError> commandProcessed) 
        where TResult : IResult<Unit, TError, TResult>
        where TError : IError<TError> =>
        commandProcessed.CommandResult
            .Match(
                processed: p => p.FunctionalResult.Match(
                    ok: _ => TResult.Ok(Unit.Default),
                    failed: error => TResult.Error(error.Error)
                ),
                unhandled: u => TResult.Error(TError.Internal(u.Message)),
                faulted: f => TResult.Error(TError.Internal(f.ToString())),
                cancelled: c => TResult.Error(TError.Cancelled(c.ToString()))
            );
}