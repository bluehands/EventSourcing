using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace EventSourcing.Commands.Infrastructure.Internal;

internal class EventReplayState<TError>(CommandBus commandBus, EventStream<Event> eventStream, ILogger<EventReplayState<TError>>? logger = null)
    : IEventReplayState where TError : notnull
{
    Task<Event>? _noopProcessed;

    internal void SendNoopCommand()
    {
        _noopProcessed = SendNoopAndWaitForProcessedEvent();
    }

    async Task<Event> SendNoopAndWaitForProcessedEvent()
    {
        var sw = Stopwatch.StartNew();
        var processed = await commandBus.SendAndWaitForProcessedEvent<TError>(new NoopCommand(), eventStream)
            .ConfigureAwait(false);
         logger?.LogInformation("Replayed done. Replayed events up to position {ReplayPosition} in {ReplayDuration} s.", processed.Position, sw.Elapsed.TotalSeconds.ToString("N3"));
         return processed;
    }

    public Task WaitForReplayDone() => _noopProcessed ?? throw new InvalidOperationException("Noop command not send. Wait can be called after in EventReplay phase of later");
}

public record NoopCommand : Command;

public class NoopCommandProcessor<TError> : SynchronousCommandProcessor<NoopCommand, TError> where TError : notnull
{
    public override ProcessingResult<TError> ProcessSync(NoopCommand command) => ProcessingResult<TError>.Ok($"Noop command {command}");
}