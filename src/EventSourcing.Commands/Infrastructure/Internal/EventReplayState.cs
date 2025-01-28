using System;
using System.Diagnostics;
using System.Reactive;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace EventSourcing.Funicular.Commands.Infrastructure.Internal;

internal class EventReplayState<TFailure, TOperationResult>(CommandBus commandBus, EventStream<Event> eventStream, ILogger<EventReplayState<TFailure, TOperationResult>>? logger = null)
    : IEventReplayState
    where TFailure : IFailure<TFailure>
    where TOperationResult : IResult<Unit, TFailure>
{
    Task<Event>? _noopProcessed;

    internal void SendNoopCommand()
    {
        _noopProcessed = SendNoopAndWaitForProcessedEvent();
    }

    async Task<Event> SendNoopAndWaitForProcessedEvent()
    {
        var sw = Stopwatch.StartNew();
        var processed = await commandBus.SendAndWaitForProcessedEvent<TFailure, TOperationResult>(new NoopCommand(), eventStream)
            .ConfigureAwait(false);
         logger?.LogInformation("Replayed done. Replayed events up to position {ReplayPosition} in {ReplayDuration} s.", processed.Position, sw.Elapsed.TotalSeconds.ToString("N3"));
         return processed;
    }

    public Task WaitForReplayDone() => _noopProcessed ?? throw new InvalidOperationException("Noop command not send. Wait can be called after in EventReplay phase of later");
}

public record NoopCommand : Command;

public class NoopCommandProcessor<TFailure> : SynchronousCommandProcessor<NoopCommand, TFailure>
    where TFailure : IFailure<TFailure>
{
    public override CommandResult<TFailure>.Processed_ ProcessSync(NoopCommand command) => command.ToEmptyProcessingResult<TFailure>($"Noop command {command}");
}