using System.Reactive;

namespace EventSourcing.Funicular.Commands.Defaults;

public static class CommandBusExtensions
{
    public static Task<OperationResult<Unit>> SendCommandAndWaitUntilApplied(this ICommandBus commandBus,
        Command command, IObservable<Event> eventStream)
        => commandBus.SendCommandAndWaitUntilApplied<Failure, OperationResult<Unit>>(command, eventStream);

    public static Task<Event<CommandProcessed<Failure, OperationResult<Unit>>>> SendAndWaitForProcessedEvent(
        this ICommandBus commandBus, Command command, IObservable<Event> events)
        => commandBus.SendAndWaitForProcessedEvent<Failure, OperationResult<Unit>>(command, events);

    public static Task<Event<CommandProcessed<Failure, OperationResult<Unit>>>> SendAndWaitForProcessedEvent(
        this ICommandBus commandBus, Command command,
        IObservable<Event<CommandProcessed<Failure, OperationResult<Unit>>>> commandProcessedEvents)
        => commandBus.SendAndWaitForProcessedEvent<Failure, OperationResult<Unit>>(command, commandProcessedEvents);
}