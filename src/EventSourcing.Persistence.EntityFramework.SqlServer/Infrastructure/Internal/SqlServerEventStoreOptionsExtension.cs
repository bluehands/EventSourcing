using EventSourcing.Infrastructure;
using EventSourcing.Infrastructure.Internal;
using EventSourcing.Persistence.EntityFramework.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EventSourcing.Persistence.EntityFramework.SqlServer.Infrastructure.Internal;

public record SqlServerEventStoreOptionsExtension(Func<IServiceProvider, string?> ConnectionString) : IEventSourcingOptionsExtension
{
    public SqlServerEventStoreOptionsExtension() : this(_ => null)
    {
    }

    public void SetDefaults(EventSourcingOptionsBuilder builder)
    {
        if (!builder.EventStreamOptionsConfigured())
        {
            new SqlServerEventStoreOptionsBuilder(builder)
                .UseBrokerNotificationEventStream();
        }
    }

    public void ApplyServices(IServiceCollection serviceCollection)
    {
        serviceCollection.AddDbContext<EventStoreContext>((sp, options) =>
            options
                .UseSqlServer(ConnectionString(sp), 
                    o => o.MigrationsAssembly(typeof(Marker).Assembly.GetName().Name!))
                .AddInterceptors([new ExclusiveWriteInterceptor()])
        );

        serviceCollection.AddEntityFrameworkServices();
    }

    public void AddDefaultServices(IServiceCollection serviceCollection, EventSourcingOptions eventSourcingOptions)
    {
    }
}