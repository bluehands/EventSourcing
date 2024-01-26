using System.Threading.Tasks;
using EventSourcing.Infrastructure;

namespace EventSourcing.Funicular.Commands.Infrastructure;

[InitializationPhase(InitializationPhaseOrders.EventReplayStarting + 100)]
public class AfterEventReplay : IInitializationPhase;

public class AfterEventReplayInitializer : IInitializer<AfterEventReplay>
{
    public virtual Task Initialize() => Task.CompletedTask;
}