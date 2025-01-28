using System.Reactive;
using EventSourcing.Funicular.Commands.Defaults.JsonPayload;
using EventSourcing.Funicular.Commands.Infrastructure;

namespace EventSourcing.Funicular.Commands.Defaults;

public static class FunicularCommandOptionsExtensions
{
    public static EventSourcingOptionsBuilder UseDefaultFunicularCommands(this EventSourcingOptionsBuilder optionsBuilder,
        Action<FunicularCommandsOptionsBuilder<Failure, FailurePayload, OperationResult<Unit>>>? funicularCommandOptionsAction = null)
        => optionsBuilder
            .UseFunicularCommands<Failure, FailurePayload, OperationResult<Unit>>(funicularCommandOptionsAction);
}