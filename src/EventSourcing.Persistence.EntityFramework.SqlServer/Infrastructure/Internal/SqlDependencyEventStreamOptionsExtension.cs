using EventSourcing.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace EventSourcing.Persistence.EntityFramework.SqlServer.Infrastructure.Internal;

public record SqlDependencyEventStreamOptionsExtension(uint? MaxRowsPerSelect) : IEventStreamOptionsExtension
{
    public SqlDependencyEventStreamOptionsExtension() : this(default(uint?))
    {
    }

    public void SetDefaults(EventSourcingOptionsBuilder builder)
    {
    }

    public void ApplyServices(IServiceCollection serviceCollection)
    {
        BrokerNotificationEventStream.AddEventStream(serviceCollection, MaxRowsPerSelect ?? 10000);
        serviceCollection.AddSingleton<IObservable<EventSourcing.Event>>(sp => sp.GetRequiredService<EventStream<EventSourcing.Event>>());
    }

    public void AddDefaultServices(IServiceCollection serviceCollection)
    {
    }
}