namespace EventSourcing.Funicular.Commands.Defaults;

public static class CommandExtensions
{
    public static CommandResult<Failure> ToEmptyProcessingResult(this Command command, string resultMessage,
        FunctionalResult<Failure>? functionalResult = null)
        => command.ToEmptyProcessingResult<Failure>(resultMessage, functionalResult);

    public static CommandResult<Failure>.Processed_ ToOkResult(this Command command, IEventPayload resultEvent, string resultMessage)
        => command.ToOkResult<Failure>(resultEvent, resultMessage);
}

public static class ResultExtensions
{
    public static CommandResult<Failure> ToProcessedResult<T>(this Result<T> result,
        Command command, string? successMessage = null)
        where T : IEventPayload
        => result.ToProcessedResult<T, Failure>(command, successMessage);

    public static CommandResult<Failure>
        ToProcessedResult<T>(this Result<T> result, Command command,
            Func<T, string?>? successMessage)
        where T : IEventPayload
        => result.ToProcessedResult<T, Failure>(command, successMessage);

    public static CommandResult<Failure>
        ToProcessedResultMulti<TCollection>(this Result<TCollection> result, Command command,
            Func<TCollection, string>? successMessage = null)
        where TCollection : IReadOnlyCollection<IEventPayload>
        => result
            .ToProcessedResultMulti<TCollection, Failure>(command, successMessage);

    public static CommandResult<Failure>
        ToProcessedResultMulti<TCollection>(
            this Result<(TCollection eventPayloads, string successMessage)> result,
            Command command)
        where TCollection : IReadOnlyCollection<IEventPayload>
        => result
            .ToProcessedResultMulti<TCollection, Failure>(command);

    public static CommandResult<Failure>
        ToProcessedResult<T>(this Result<(T eventPayload, string successMessage)> result,
            Command command)
        where T : EventPayload
        => result.ToProcessedResult<T, Failure>(command);
}