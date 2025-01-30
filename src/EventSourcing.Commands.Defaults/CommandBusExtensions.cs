using System.Reactive;
using System.Reactive.Linq;

namespace EventSourcing.Funicular.Commands.Defaults;

public static class CommandBusExtensions
{
    public static Task<Result<Unit>> SendCommandAndWaitUntilApplied(this ICommandBus commandBus,
        Command command, IObservable<Event> eventStream)
        => commandBus.SendCommandAndWaitUntilApplied(command, eventStream.OfType<Event<CommandProcessed<Failure>>>());

    public static async Task<Result<Unit>> SendCommandAndWaitUntilApplied(this ICommandBus commandBus,
        Command command, IObservable<Event<CommandProcessed<Failure>>> commandProcessedEvents)
        => (await commandBus.SendAndWaitForProcessedEvent<Failure>(command, commandProcessedEvents)).Payload.ToResult<Result<Unit>>();

    public static Task<Event<CommandProcessed<Failure>>> SendAndWaitForProcessedEvent(
        this ICommandBus commandBus, Command command, IObservable<Event> events)
        => commandBus.SendAndWaitForProcessedEvent<Failure>(command, events);

    public static Task<Event<CommandProcessed<Failure>>> SendAndWaitForProcessedEvent(
        this ICommandBus commandBus, Command command,
        IObservable<Event<CommandProcessed<Failure>>> commandProcessedEvents)
        => commandBus.SendAndWaitForProcessedEvent<Failure>(command, commandProcessedEvents);
}