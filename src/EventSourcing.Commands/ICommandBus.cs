using System.Reactive.Concurrency;
using System.Threading;
using System;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;

namespace EventSourcing.Funicular.Commands;

public interface ICommandBus
{
    Task SendCommand(Command command);
}

public static class CommandBusExtension
{
    public static Task<Event<CommandProcessed<TFailure>>> SendAndWaitForProcessedEvent<TFailure>(this ICommandBus commandBus, Command command, IObservable<Event> events)
        where TFailure : IFailure<TFailure>
        => commandBus.SendAndWaitForProcessedEvent(command, events
            .OfType<Event<CommandProcessed<TFailure>>>());

    public static async Task<Event<CommandProcessed<TFailure>>> SendAndWaitForProcessedEvent<TFailure>(this ICommandBus commandBus, Command command, IObservable<Event<CommandProcessed<TFailure>>> commandProcessedEvents)
        where TFailure : IFailure<TFailure>
    {
        var processed = commandProcessedEvents
            .FirstAsync(e => e.Payload.CommandId == command.Id)
            .ToTask(CancellationToken.None, Scheduler.Default); //this is needed if we might be called from sync / async mixtures (https://blog.stephencleary.com/2012/12/dont-block-in-asynchronous-code.html)
        await commandBus.SendCommand(command).ConfigureAwait(false);

        var commandProcessed = await processed.ConfigureAwait(false);
        return commandProcessed;
    }
}