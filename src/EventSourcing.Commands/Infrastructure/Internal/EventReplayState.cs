using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace EventSourcing.Funicular.Commands.Infrastructure.Internal;

public class EventReplayState(CommandStream commandStream, EventStream<Event> eventStream, ILogger<EventReplayState>? logger = null)
{
    Task<Event>? _noopProcessed;

    internal void SendNoopCommand()
    {
        _noopProcessed = SendNoopAndWaitForProcessedEvent();
    }

    async Task<Event> SendNoopAndWaitForProcessedEvent()
    {
        var sw = Stopwatch.StartNew();
        var processed = await commandStream.SendAndWaitForProcessedEvent(new NoopCommand(), eventStream)
            .ConfigureAwait(false);
         logger?.LogInformation("Replayed done. Replayed events up to position {ReplayPosition} in {ReplayDuration} s.", processed.Position, sw.Elapsed.TotalSeconds.ToString("N3"));
         return processed;
    }

    public Task WaitForReplayDone() => _noopProcessed ?? throw new InvalidOperationException("Noop command not send. Wait can be called after in EventReplay phase of later");
}

public record NoopCommand : Command;

public class NoopCommandProcessor : SynchronousCommandProcessor<NoopCommand>
{
    public override CommandResult.Processed_ ProcessSync(NoopCommand command) => command.ToEmptyProcessingResult($"Noop command {command}");
}