using System;
using System.Collections.Generic;

namespace EventSourcing.Funicular.Commands;

public static class CommandExtensions
{
    public static CommandResult.Processed_ ToFailureResult(this Command command, Failure failure, string resultMessage)
        => command.ToEmptyProcessingResult(resultMessage, FunctionalResult.Failed(failure));

    public static CommandResult.Processed_ ToNotFoundResult(this Command command, string resultMessage)
        => command.ToFailureResult(Failure.NotFound(resultMessage), resultMessage);

    public static CommandResult.Processed_ ToForbiddenResult(this Command command, string resultMessage)
        => command.ToFailureResult(Failure.Forbidden(resultMessage), resultMessage);

    public static CommandResult.Processed_ ToConflictResult(this Command command, string resultMessage)
        => command.ToFailureResult(Failure.Conflict(resultMessage), resultMessage);

    public static CommandResult.Processed_ ToEmptyProcessingResult(this Command command, string resultMessage, FunctionalResult? functionalResult = null)
        => command.ToProcessedResult(Array.Empty<IEventPayload>(), functionalResult ?? FunctionalResult.Ok(resultMessage));

    public static CommandResult.Processed_ ToOkResult(this Command command, IEventPayload resultEvent, string resultMessage)
        => command.ToProcessedResult(new[] { resultEvent }, FunctionalResult.Ok(resultMessage));

    public static CommandResult.Processed_ ToProcessedResult(this Command command, IEventPayload resultEvent, FunctionalResult functionalResult)
        => command.ToProcessedResult(new[] { resultEvent }, functionalResult);

    public static CommandResult.Processed_ ToProcessedResult(this Command command,
        IReadOnlyCollection<IEventPayload> resultEvents, FunctionalResult functionalResult) =>
        new(resultEvents, command.Id, functionalResult);

    internal static string DefaultOkMessage(this Command command) => $"Successfully processed command {command}";
}

public static partial class OperationResultExtensions
{
    public static CommandResult.Processed_ ToProcessedResult<T>(this OperationResult<T> operationResult, Command command, string? successMessage = null) where T : IEventPayload
        => operationResult.ToProcessedResult(command, successMessage != null ? _ => successMessage : null);

    public static CommandResult.Processed_
        ToProcessedResult<T>(this OperationResult<T> operationResult, Command command, Func<T, string?>? successMessage) where T : IEventPayload
        => operationResult
            .Match(
                ok => command.ToProcessedResult(ok, FunctionalResult.Ok(successMessage?.Invoke(ok) ?? command.DefaultOkMessage())),
                failure => command.ToFailureResult(failure, failure.Message)
            );

    public static CommandResult.Processed_
        ToProcessedResultMulti<TCollection>(this OperationResult<TCollection> operationResult, Command command, Func<TCollection, string>? successMessage = null) where TCollection : IReadOnlyCollection<IEventPayload>
        => operationResult
            .Match(
                ok => command.ToProcessedResult(ok, FunctionalResult.Ok(successMessage?.Invoke(ok) ?? command.DefaultOkMessage())),
                failure => command.ToFailureResult(failure, failure.Message)
            );
}