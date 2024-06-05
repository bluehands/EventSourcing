using EventSourcing.Infrastructure;

namespace EventSourcing.Funicular.Commands.Infrastructure;

[InitializationPhase(InitializationPhaseOrders.EventReplayStarting + 100)]
public class AfterEventReplay : IInitializationPhase;

public interface IAfterEventReplayInitializer : IInitializer<AfterEventReplay>
{
}