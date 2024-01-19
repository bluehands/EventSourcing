namespace EventSourcing.Infrastructure;

public interface IEventSourcingBuilderInfrastructure
{
    void AddOrUpdateExtension<TExtension>(TExtension extension) where TExtension : class, IEventSourcingOptionsExtension;
}