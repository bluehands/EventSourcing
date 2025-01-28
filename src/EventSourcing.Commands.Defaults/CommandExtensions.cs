namespace EventSourcing.Funicular.Commands.Defaults;

public static class CommandExtensions
{
    public static CommandResult<Failure> ToFailureResult(this Command command, Failure failure,
        string resultMessage)
        => command.ToFailureResult<Failure>(failure, resultMessage);

    public static CommandResult<Failure> ToEmptyProcessingResult(this Command command, string resultMessage,
        FunctionalResult<Failure>? functionalResult = null)
        => command.ToEmptyProcessingResult<Failure>(resultMessage, functionalResult);

    public static CommandResult<Failure>.Processed_ ToOkResult(this Command command, IEventPayload resultEvent, string resultMessage)
        => command.ToOkResult<Failure>(resultEvent, resultMessage);

    public static CommandResult<Failure> ToProcessedResult(this Command command, IEventPayload resultEvent,
        FunctionalResult<Failure> functionalResult)
        => command.ToProcessedResult<Failure>(resultEvent, functionalResult);

    public static CommandResult<Failure> ToProcessedResult(this Command command,
        IReadOnlyCollection<IEventPayload> resultEvents, FunctionalResult<Failure> functionalResult)
        => command.ToProcessedResult<Failure>(resultEvents, functionalResult);
}

public static class DefaultOperationResultExtensions
{
    public static CommandResult<Failure> ToProcessedResult<T>(this OperationResult<T> operationResult,
        Command command, string? successMessage = null)
        where T : IEventPayload
        => operationResult.ToProcessedResult<T, Failure>(command, successMessage);

    public static CommandResult<Failure>
        ToProcessedResult<T>(this OperationResult<T> operationResult, Command command,
            Func<T, string?>? successMessage)
        where T : IEventPayload
        => operationResult.ToProcessedResult<T, Failure>(command, successMessage);

    public static CommandResult<Failure>
        ToProcessedResultMulti<TCollection>(this OperationResult<TCollection> operationResult, Command command,
            Func<TCollection, string>? successMessage = null)
        where TCollection : IReadOnlyCollection<IEventPayload>
        => operationResult
            .ToProcessedResultMulti<TCollection, Failure>(command, successMessage);

    public static CommandResult<Failure>
        ToProcessedResultMulti<TCollection>(
            this OperationResult<(TCollection eventPayloads, string successMessage)> operationResult,
            Command command)
        where TCollection : IReadOnlyCollection<IEventPayload>
        => operationResult
            .ToProcessedResultMulti<TCollection, Failure>(command);

    public static CommandResult<Failure>
        ToProcessedResult<T>(this OperationResult<(T eventPayload, string successMessage)> operationResult,
            Command command)
        where T : EventPayload
        => operationResult.ToProcessedResult<T, Failure>(command);
}