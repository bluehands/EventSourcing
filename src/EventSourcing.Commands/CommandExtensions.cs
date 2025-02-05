using System;
using System.Collections.Generic;

namespace EventSourcing.Commands;

public static class ResultExtensions
{
    public static ProcessingResult<TError> ToProcessingResult<T, TError>(this IResult<T, TError> result, string? successMessage = null)
        where T : IEventPayload
        => result.ToProcessingResult(successMessage != null ? _ => successMessage : null);

    public static ProcessingResult<TError>
        ToProcessingResult<T, TError>(this IResult<T, TError> result, Func<T, string?>? successMessage)
        where T : IEventPayload
        => result.Match<ProcessingResult<TError>>(
                ok => ProcessingResult<TError>.Ok([ok], successMessage?.Invoke(ok)),
                error => ProcessingResult<TError>.Failed([], error)
            );

    public static ProcessingResult<TError>
        ToProcessedResultMulti<TCollection, TError>(this IResult<TCollection, TError> result, Func<TCollection, string>? successMessage = null)
        where TCollection : IReadOnlyCollection<IEventPayload>
        => result.Match<ProcessingResult<TError>>(
                ok => ProcessingResult<TError>.Ok(ok, successMessage?.Invoke(ok)),
                error => ProcessingResult<TError>.Failed([], error)
            );

    public static ProcessingResult<TError>
        ToProcessedResultMulti<TCollection, TError>(this IResult<(TCollection eventPayloads, string successMessage), TError> result)
        where TCollection : IReadOnlyCollection<IEventPayload>
        => result.Match<ProcessingResult<TError>>(
                ok => ProcessingResult<TError>.Ok(ok.eventPayloads, ok.successMessage),
                error => ProcessingResult<TError>.Failed([], error)
            );

    public static ProcessingResult<TError>
        ToProcessingResult<T, TError>(this IResult<(T eventPayload, string successMessage), TError> result)
        where T : EventPayload
        => result.Match<ProcessingResult<TError>>(
                ok => ProcessingResult<TError>.Ok([ok.eventPayload], ok.successMessage),
                error => ProcessingResult<TError>.Failed([], error)
            );
}