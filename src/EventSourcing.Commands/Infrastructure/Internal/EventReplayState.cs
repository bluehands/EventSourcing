using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace EventSourcing.Commands.Infrastructure.Internal;

internal class EventReplayState<TFailure>(CommandBus commandBus, EventStream<Event> eventStream, ILogger<EventReplayState<TFailure>>? logger = null)
    : IEventReplayState where TFailure : notnull
{
    Task<Event>? _noopProcessed;

    internal void SendNoopCommand()
    {
        _noopProcessed = SendNoopAndWaitForProcessedEvent();
    }

    async Task<Event> SendNoopAndWaitForProcessedEvent()
    {
        var sw = Stopwatch.StartNew();
        var processed = await commandBus.SendAndWaitForProcessedEvent<TFailure>(new NoopCommand(), eventStream)
            .ConfigureAwait(false);
         logger?.LogInformation("Replayed done. Replayed events up to position {ReplayPosition} in {ReplayDuration} s.", processed.Position, sw.Elapsed.TotalSeconds.ToString("N3"));
         return processed;
    }

    public Task WaitForReplayDone() => _noopProcessed ?? throw new InvalidOperationException("Noop command not send. Wait can be called after in EventReplay phase of later");
}

public record NoopCommand : Command;

public class NoopCommandProcessor<TFailure> : SynchronousCommandProcessor<NoopCommand, TFailure> where TFailure : notnull
{
    public override ProcessingResult<TFailure> ProcessSync(NoopCommand command) => ProcessingResult<TFailure>.Ok($"Noop command {command}");
}