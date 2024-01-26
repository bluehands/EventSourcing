using System;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using EventSourcing.Infrastructure;

namespace EventSourcing.Funicular.Commands.Infrastructure.Internal;

public class EventReplayState(CommandStream commandStream, EventStream<Event> eventStream)
{
    Task<OperationResult<Unit>>? _noopProcessed;

    internal void SendNoopCommand()
    {
        _noopProcessed = commandStream.SendCommandAndWaitUntilApplied(new NoopCommand(),
            eventStream.Select(e => e.Payload).OfType<CommandProcessed>());
    }

    public Task WaitForReplayDone() => _noopProcessed ?? throw new InvalidOperationException(
        "Noop command not send. Wait can be called after in EventReplay phase of later");
}

public record NoopCommand : Command;

public class NoopCommandProcessor : SynchronousCommandProcessor<NoopCommand>
{
    public override CommandResult.Processed_ ProcessSync(NoopCommand command) => new(new EventPayload[] { }, command.Id, FunctionalResult.Ok("Noop"));
}