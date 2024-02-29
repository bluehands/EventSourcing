using System;
using System.Linq;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using AsyncLock = EventSourcing.Funicular.Commands.Infrastructure.Internal.AsyncLock;
using EventSourcing.Funicular.Commands.Infrastructure.Internal;

namespace EventSourcing.Funicular.Commands;

public sealed class CommandStream : IObservable<Command>, IDisposable
{
    readonly AsyncLock _lock = new();
    readonly Subject<Command> _innerStream;
    readonly IObservable<Command> _commands;

    public CommandStream()
    {
        _innerStream = new();
        _commands = _innerStream.Publish().RefCount();
    }

    public IDisposable Subscribe(IObserver<Command> observer) => _commands.Subscribe(observer);

    public async Task SendCommand(Command command) => await _lock.ExecuteGuarded(() => _innerStream.OnNext(command)).ConfigureAwait(false);

    public Task SendCommands(IEnumerable<Command> commands) => Task.WhenAll(commands.Select(SendCommand));

    public void Dispose()
    {
        _lock.Dispose();
        _innerStream.Dispose();
    }
}

public static class CommandStreamExtension
{
    public static async Task<OperationResult<Unit>> SendCommandAndWaitUntilApplied(this CommandStream commandStream, Command command, IObservable<Event> eventStream)
    {
        var commandProcessed = await SendAndWaitForProcessedEvent(commandStream, command, eventStream).ConfigureAwait(false);
        return commandProcessed.Payload.OperationResult;
    }

    public static async Task<Event<CommandProcessed>> SendAndWaitForProcessedEvent(this CommandStream commandStream, Command command, IObservable<Event> events)
    {
        var processed = events
            .OfType<Event<CommandProcessed>>()
            .FirstAsync(e => e.Payload.CommandId == command.Id)
            .ToTask(CancellationToken.None, Scheduler.Default); //this is needed if we might be called from sync / async mixtures (https://blog.stephencleary.com/2012/12/dont-block-in-asynchronous-code.html)
        await commandStream.SendCommand(command).ConfigureAwait(false);

        var commandProcessed = await processed.ConfigureAwait(false);
        return commandProcessed;
    }
}

public static partial class StreamIds
{
    public static readonly StreamId Command = new("Command", "Command");
}

public static partial class EventTypes
{
    public const string CommandProcessed = "CommandProcessed";
}

public record CommandProcessed(CommandId CommandId, OperationResult<Unit> OperationResult, string? ResultMessage)
    : EventPayload(StreamIds.Command, EventTypes.CommandProcessed)
{
    public override string ToString() => $"{CommandId} processed with result {OperationResult}: {ResultMessage}";
}