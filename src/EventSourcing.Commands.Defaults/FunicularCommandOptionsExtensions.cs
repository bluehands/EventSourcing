using EventSourcing.Funicular.Commands.Defaults.SerializablePayloads;
using EventSourcing.Funicular.Commands.Infrastructure;

namespace EventSourcing.Funicular.Commands.Defaults;

public static class FunicularCommandOptionsExtensions
{
    public static EventSourcingOptionsBuilder UseFunicularCommands(this EventSourcingOptionsBuilder optionsBuilder,
        Action<FunicularCommandsOptionsBuilder<Failure, FailurePayload>>? funicularCommandOptionsAction = null)
        => optionsBuilder
            .UseFunicularCommands<Failure, FailurePayload>(funicularCommandOptionsAction);
}