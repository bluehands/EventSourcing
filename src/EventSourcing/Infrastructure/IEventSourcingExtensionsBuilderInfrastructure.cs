namespace EventSourcing.Infrastructure;

public interface IEventSourcingExtensionsBuilderInfrastructure
{
    EventSourcingOptionsBuilder OptionsBuilder { get; }
}