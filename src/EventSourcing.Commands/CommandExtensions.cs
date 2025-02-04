using System;
using System.Collections.Generic;

namespace EventSourcing.Commands;

public static class ResultExtensions
{
    public static ProcessingResult<TFailure> ToProcessingResult<T, TFailure>(this IResult<T, TFailure> result, string? successMessage = null)
        where T : IEventPayload
        => result.ToProcessingResult(successMessage != null ? _ => successMessage : null);

    public static ProcessingResult<TFailure>
        ToProcessingResult<T, TFailure>(this IResult<T, TFailure> result, Func<T, string?>? successMessage)
        where T : IEventPayload
        => result.Match<ProcessingResult<TFailure>>(
                ok => ProcessingResult<TFailure>.Ok([ok], successMessage?.Invoke(ok)),
                failure => ProcessingResult<TFailure>.Failed([], failure)
            );

    public static ProcessingResult<TFailure>
        ToProcessedResultMulti<TCollection, TFailure>(this IResult<TCollection, TFailure> result, Func<TCollection, string>? successMessage = null)
        where TCollection : IReadOnlyCollection<IEventPayload>
        => result.Match<ProcessingResult<TFailure>>(
                ok => ProcessingResult<TFailure>.Ok(ok, successMessage?.Invoke(ok)),
                failure => ProcessingResult<TFailure>.Failed([], failure)
            );

    public static ProcessingResult<TFailure>
        ToProcessedResultMulti<TCollection, TFailure>(this IResult<(TCollection eventPayloads, string successMessage), TFailure> result)
        where TCollection : IReadOnlyCollection<IEventPayload>
        => result.Match<ProcessingResult<TFailure>>(
                ok => ProcessingResult<TFailure>.Ok(ok.eventPayloads, ok.successMessage),
                failure => ProcessingResult<TFailure>.Failed([], failure)
            );

    public static ProcessingResult<TFailure>
        ToProcessingResult<T, TFailure>(this IResult<(T eventPayload, string successMessage), TFailure> result)
        where T : EventPayload
        => result.Match<ProcessingResult<TFailure>>(
                ok => ProcessingResult<TFailure>.Ok([ok.eventPayload], ok.successMessage),
                failure => ProcessingResult<TFailure>.Failed([], failure)
            );
}