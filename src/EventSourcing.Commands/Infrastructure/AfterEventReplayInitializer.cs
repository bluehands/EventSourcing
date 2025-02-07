using EventSourcing.Infrastructure;

namespace EventSourcing.Commands.Infrastructure;

[InitializationPhase(InitializationPhaseOrders.EventReplayStarted + 100)]
public class AfterEventReplay : IInitializationPhase;

public interface IAfterEventReplayInitializer : IInitializer<AfterEventReplay>
{
}