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
                .UsePollingEventStream();
        }
    }

    public void ApplyServices(IServiceCollection serviceCollection)
    {
        serviceCollection.AddDbContext<EventStoreContext, SqlServerEventStoreContext>((sp, options) =>
            options
                .UseSqlServer(ConnectionString(sp), 
                    o => o.MigrationsAssembly(typeof(Marker).Assembly.GetName().Name!))
        );

        serviceCollection.AddTransient<IEventWriter<Event>, SqlServerEventWriter>();
        serviceCollection.AddEntityFrameworkServices();
    }

    public void AddDefaultServices(IServiceCollection serviceCollection, EventSourcingOptions eventSourcingOptions)
    {
    }
}
