using System.Threading.Tasks;

namespace EventSourcing.Infrastructure;

public interface IInitializer
{
    Task Initialize();
}

// ReSharper disable once UnusedTypeParameter
public interface IInitializer<TPhase> : IInitializer where TPhase : IInitializationPhase;

public interface IInitializationPhase;

public sealed class SchemaInitialization : IInitializationPhase;
public sealed class BeforeEventsReplay : IInitializationPhase;