using System;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;

namespace EventSourcing.Commands;

public interface ICommandBus
{
    Task SendCommand(Command command);
}

public static class CommandBusExtension
{
    public static Task<Event<CommandProcessed<TError>>> SendAndWaitForProcessedEvent<TError>(this ICommandBus commandBus, Command command, IObservable<Event> events) where TError : notnull => commandBus.SendAndWaitForProcessedEvent(command, events
            .OfType<Event<CommandProcessed<TError>>>());

    public static async Task<Event<CommandProcessed<TError>>> SendAndWaitForProcessedEvent<TError>(this ICommandBus commandBus, Command command, IObservable<Event<CommandProcessed<TError>>> commandProcessedEvents) where TError : notnull
    {
        var processed = commandProcessedEvents
            .FirstAsync(e => e.Payload.CommandId == command.Id)
            .ToTask(CancellationToken.None, Scheduler.Default); //this is needed if we might be called from sync / async mixtures (https://blog.stephencleary.com/2012/12/dont-block-in-asynchronous-code.html)
        await commandBus.SendCommand(command).ConfigureAwait(false);

        var commandProcessed = await processed.ConfigureAwait(false);
        return commandProcessed;
    }
}