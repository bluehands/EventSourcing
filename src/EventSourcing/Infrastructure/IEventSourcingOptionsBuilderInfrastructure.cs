namespace EventSourcing.Infrastructure;

public interface IEventSourcingOptionsBuilderInfrastructure
{
    void AddOrUpdateExtension<TExtension>(TExtension extension) where TExtension : class, IEventSourcingOptionsExtension;
}