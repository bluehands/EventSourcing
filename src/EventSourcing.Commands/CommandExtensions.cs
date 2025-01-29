using System;
using System.Collections.Generic;

namespace EventSourcing.Funicular.Commands;

public static class CommandExtensions
{
    public static CommandResult<TFailure>.Processed_ ToFailureResult<TFailure>(this Command command, TFailure failure, string resultMessage)
        where TFailure : IFailure<TFailure>
        => command.ToEmptyProcessingResult(resultMessage, FunctionalResult<TFailure>.Failed(failure));
    
    public static CommandResult<TFailure>.Processed_ ToEmptyProcessingResult<TFailure>(this Command command, string resultMessage, FunctionalResult<TFailure>? functionalResult = null)
        where TFailure : IFailure<TFailure>
        => command.ToProcessedResult(Array.Empty<IEventPayload>(), functionalResult ?? FunctionalResult<TFailure>.Ok(resultMessage));

    public static CommandResult<TFailure>.Processed_ ToOkResult<TFailure>(this Command command, IEventPayload resultEvent, string resultMessage)
        where TFailure : IFailure<TFailure>
        => command.ToProcessedResult([resultEvent], FunctionalResult<TFailure>.Ok(resultMessage));

    public static CommandResult<TFailure>.Processed_ ToProcessedResult<TFailure>(this Command command, IEventPayload resultEvent, FunctionalResult<TFailure> functionalResult)
        where TFailure : IFailure<TFailure>
        => command.ToProcessedResult([resultEvent], functionalResult);

    public static CommandResult<TFailure>.Processed_ ToProcessedResult<TFailure>(this Command command,
        IReadOnlyCollection<IEventPayload> resultEvents, FunctionalResult<TFailure> functionalResult)
        where TFailure : IFailure<TFailure> =>
        new(resultEvents, command.Id, functionalResult);

    internal static string DefaultOkMessage(this Command command) => $"Successfully processed command {command}";
}

public static class ResultExtensions
{
    public static CommandResult<TFailure>.Processed_ ToProcessedResult<T, TFailure>(this IResult<T, TFailure> result, Command command, string? successMessage = null)
        where T : IEventPayload
        where TFailure : IFailure<TFailure>
        => result.ToProcessedResult(command, successMessage != null ? _ => successMessage : null);

    public static CommandResult<TFailure>.Processed_
        ToProcessedResult<T, TFailure>(this IResult<T, TFailure> result, Command command, Func<T, string?>? successMessage)
        where T : IEventPayload
        where TFailure : IFailure<TFailure>
        => result
            .Match(
                ok => command.ToProcessedResult(ok, FunctionalResult<TFailure>.Ok(successMessage?.Invoke(ok) ?? command.DefaultOkMessage())),
                failure => command.ToFailureResult(failure, failure.Message)
            );

    public static CommandResult<TFailure>.Processed_
        ToProcessedResultMulti<TCollection, TFailure>(this IResult<TCollection, TFailure> result, Command command, Func<TCollection, string>? successMessage = null)
        where TCollection : IReadOnlyCollection<IEventPayload>
        where TFailure : IFailure<TFailure>
        => result
            .Match(
                ok => command.ToProcessedResult(ok, FunctionalResult<TFailure>.Ok(successMessage?.Invoke(ok) ?? command.DefaultOkMessage())),
                failure => command.ToFailureResult(failure, failure.Message)
            );

    public static CommandResult<TFailure>.Processed_
        ToProcessedResultMulti<TCollection, TFailure>(this IResult<(TCollection eventPayloads, string successMessage), TFailure> result, Command command)
        where TCollection : IReadOnlyCollection<IEventPayload>
        where TFailure : IFailure<TFailure>
        => result
            .Match(
                ok => command.ToProcessedResult(ok.eventPayloads, FunctionalResult<TFailure>.Ok(ok.successMessage)),
                failure => command.ToFailureResult(failure, failure.Message)
            );

    public static CommandResult<TFailure>.Processed_
        ToProcessedResult<T, TFailure>(this IResult<(T eventPayload, string successMessage), TFailure> result, Command command)
        where T : EventPayload
        where TFailure : IFailure<TFailure>
        => result
            .Match(
                ok => command.ToProcessedResult(ok.eventPayload, FunctionalResult<TFailure>.Ok(ok.successMessage)),
                failure => command.ToFailureResult(failure, failure.Message)
            );
}