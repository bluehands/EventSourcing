using System.Reactive;

namespace EventSourcing.Funicular.Commands.Defaults;

public static class CommandBusExtensions
{
    public static Task<Result<Unit>> SendCommandAndWaitUntilApplied(this ICommandBus commandBus,
        Command command, IObservable<Event> eventStream)
        => commandBus.SendCommandAndWaitUntilApplied<Failure, Result<Unit>>(command, eventStream);

    public static Task<Event<CommandProcessed<Failure, Result<Unit>>>> SendAndWaitForProcessedEvent(
        this ICommandBus commandBus, Command command, IObservable<Event> events)
        => commandBus.SendAndWaitForProcessedEvent<Failure, Result<Unit>>(command, events);

    public static Task<Event<CommandProcessed<Failure, Result<Unit>>>> SendAndWaitForProcessedEvent(
        this ICommandBus commandBus, Command command,
        IObservable<Event<CommandProcessed<Failure, Result<Unit>>>> commandProcessedEvents)
        => commandBus.SendAndWaitForProcessedEvent<Failure, Result<Unit>>(command, commandProcessedEvents);
}