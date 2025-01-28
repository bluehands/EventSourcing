using System;
using System.Collections.Generic;

namespace EventSourcing.Funicular.Commands;

public static class CommandExtensions
{
    public static CommandResult<TFailure>.Processed_ ToFailureResult<TFailure>(this Command command, TFailure failure, string resultMessage)
        where TFailure : IFailure<TFailure>
        => command.ToEmptyProcessingResult<TFailure>(resultMessage, FunctionalResult<TFailure>.Failed(failure));
    
    public static CommandResult<TFailure>.Processed_ ToEmptyProcessingResult<TFailure>(this Command command, string resultMessage, FunctionalResult<TFailure>? functionalResult = null)
        where TFailure : IFailure<TFailure>
        => command.ToProcessedResult<TFailure>(Array.Empty<IEventPayload>(), functionalResult ?? FunctionalResult<TFailure>.Ok(resultMessage));

    public static CommandResult<TFailure>.Processed_ ToOkResult<TFailure>(this Command command, IEventPayload resultEvent, string resultMessage)
        where TFailure : IFailure<TFailure>
        => command.ToProcessedResult<TFailure>([resultEvent], FunctionalResult<TFailure>.Ok(resultMessage));

    public static CommandResult<TFailure>.Processed_ ToProcessedResult<TFailure>(this Command command, IEventPayload resultEvent, FunctionalResult<TFailure> functionalResult)
        where TFailure : IFailure<TFailure>
        => command.ToProcessedResult<TFailure>([resultEvent], functionalResult);

    public static CommandResult<TFailure>.Processed_ ToProcessedResult<TFailure>(this Command command,
        IReadOnlyCollection<IEventPayload> resultEvents, FunctionalResult<TFailure> functionalResult)
        where TFailure : IFailure<TFailure>
        => new CommandResult<TFailure>.Processed_(resultEvents, command.Id, functionalResult);

    internal static string DefaultOkMessage(this Command command) => $"Successfully processed command {command}";
}

public static partial class OperationResultExtensions
{
    public static CommandResult<TFailure> ToProcessedResult<T, TFailure>(this IResult<T, TFailure> operationResult, Command command, string? successMessage = null)
        where T : IEventPayload
        where TFailure : IFailure<TFailure>
        => operationResult.ToProcessedResult<T, TFailure>(command, successMessage != null ? _ => successMessage : null);

    public static CommandResult<TFailure>
        ToProcessedResult<T, TFailure>(this IResult<T, TFailure> operationResult, Command command, Func<T, string?>? successMessage)
        where T : IEventPayload
        where TFailure : IFailure<TFailure>
        => operationResult
            .Match(
                ok => command.ToProcessedResult<TFailure>(ok, FunctionalResult<TFailure>.Ok(successMessage?.Invoke(ok) ?? command.DefaultOkMessage())),
                failure => command.ToFailureResult<TFailure>(failure, failure.Message)
            );

    public static CommandResult<TFailure>
        ToProcessedResultMulti<TCollection, TFailure>(this IResult<TCollection, TFailure> operationResult, Command command, Func<TCollection, string>? successMessage = null)
        where TCollection : IReadOnlyCollection<IEventPayload>
        where TFailure : IFailure<TFailure>
        => operationResult
            .Match(
                ok => command.ToProcessedResult<TFailure>(ok, FunctionalResult<TFailure>.Ok(successMessage?.Invoke(ok) ?? command.DefaultOkMessage())),
                failure => command.ToFailureResult<TFailure>(failure, failure.Message)
            );

    public static CommandResult<TFailure>
        ToProcessedResultMulti<TCollection, TFailure>(this IResult<(TCollection eventPayloads, string successMessage), TFailure> operationResult, Command command)
        where TCollection : IReadOnlyCollection<IEventPayload>
        where TFailure : IFailure<TFailure>
        => operationResult
            .Match(
                ok => command.ToProcessedResult<TFailure>(ok.eventPayloads, FunctionalResult<TFailure>.Ok(ok.successMessage)),
                failure => command.ToFailureResult<TFailure>(failure, failure.Message)
            );

    public static CommandResult<TFailure>
        ToProcessedResult<T, TFailure>(this IResult<(T eventPayload, string successMessage), TFailure> operationResult, Command command)
        where T : EventPayload
        where TFailure : IFailure<TFailure>
        => operationResult
            .Match(
                ok => command.ToProcessedResult<TFailure>(ok.eventPayload, FunctionalResult<TFailure>.Ok(ok.successMessage)),
                failure => command.ToFailureResult<TFailure>(failure, failure.Message)
            );
}