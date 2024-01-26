using EventSourcing.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace EventSourcing.Persistence.EntityFramework.SqlServer.Infrastructure.Internal;

public record SqlDependencyEventStreamOptionsExtension(uint? MaxRowsPerSelect, Func<Task<long>>? GetPositionToStartFrom) : IEventStreamOptionsExtension
{
    public SqlDependencyEventStreamOptionsExtension() : this(null, null)
    {
    }

    public void SetDefaults(EventSourcingOptionsBuilder builder)
    {
    }

    public void ApplyServices(IServiceCollection serviceCollection)
    {
        BrokerNotificationEventStream.AddEventStream(serviceCollection, MaxRowsPerSelect ?? 10000, GetPositionToStartFrom ?? (() =>Task.FromResult(0L)));
        serviceCollection.AddSingleton<IObservable<EventSourcing.Event>>(sp => sp.GetRequiredService<EventStream<EventSourcing.Event>>());
    }

    public void AddDefaultServices(IServiceCollection serviceCollection, EventSourcingOptions eventSourcingOptions)
    {
    }
}