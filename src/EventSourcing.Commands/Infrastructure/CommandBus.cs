using System;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using AsyncLock = EventSourcing.Funicular.Commands.Infrastructure.Internal.AsyncLock;
using EventSourcing.Funicular.Commands.Infrastructure.Internal;

namespace EventSourcing.Funicular.Commands.Infrastructure;

public sealed class CommandBus : IObservable<Command>, IDisposable, ICommandBus
{
    readonly AsyncLock _lock = new();
    readonly Subject<Command> _innerStream;
    readonly IObservable<Command> _commands;

    public CommandBus()
    {
        _innerStream = new();
        _commands = _innerStream.Publish().RefCount();
    }

    public IDisposable Subscribe(IObserver<Command> observer) => _commands.Subscribe(observer);

    public async Task SendCommand(Command command) => await _lock.ExecuteGuarded(() => _innerStream.OnNext(command)).ConfigureAwait(false);

    public void Dispose()
    {
        _lock.Dispose();
        _innerStream.Dispose();
    }
}

public static class CommandStreamExtension
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

public static class StreamIds
{
    public static readonly StreamId Command = new("Command", "Command");
}

public static class EventTypes
{
    public const string CommandProcessed = "CommandProcessed";
}

public record CommandProcessed(CommandId CommandId, OperationResult<Unit> OperationResult, string? ResultMessage)
    : EventPayload(StreamIds.Command, EventTypes.CommandProcessed)
{
    public override string ToString() => $"{CommandId} processed with result {OperationResult}: {ResultMessage}";
}