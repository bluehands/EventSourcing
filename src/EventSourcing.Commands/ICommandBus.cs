using System.Reactive.Concurrency;
using System.Reactive;
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
    public static async Task<OperationResult<Unit>> SendCommandAndWaitUntilApplied(this ICommandBus commandBus, Command command, IObservable<Event> eventStream)
    {
        var commandProcessed = await commandBus.SendAndWaitForProcessedEvent(command, eventStream).ConfigureAwait(false);
        return commandProcessed.Payload.OperationResult;
    }

    public static Task<Event<CommandProcessed>> SendAndWaitForProcessedEvent(this ICommandBus commandBus, Command command, IObservable<Event> events) =>
        commandBus.SendAndWaitForProcessedEvent(command, events
            .OfType<Event<CommandProcessed>>());

    public static async Task<Event<CommandProcessed>> SendAndWaitForProcessedEvent(this ICommandBus commandBus, Command command, IObservable<Event<CommandProcessed>> commandProcessedEvents)
    {
        var processed = commandProcessedEvents
            .FirstAsync(e => e.Payload.CommandId == command.Id)
            .ToTask(CancellationToken.None, Scheduler.Default); //this is needed if we might be called from sync / async mixtures (https://blog.stephencleary.com/2012/12/dont-block-in-asynchronous-code.html)
        await commandBus.SendCommand(command).ConfigureAwait(false);

        var commandProcessed = await processed.ConfigureAwait(false);
        return commandProcessed;
    }
}