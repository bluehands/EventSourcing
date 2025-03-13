using EventSourcing.Infrastructure;
using EventSourcing.Infrastructure.Internal;
using EventSourcing.Persistence.EntityFramework.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EventSourcing.Persistence.EntityFramework.Sqlite.Infrastructure.Internal;

public record SqliteEventStoreOptionsExtension(Func<IServiceProvider, string?> ConnectionString) : IEventSourcingOptionsExtension
{
    public SqliteEventStoreOptionsExtension() : this(_ => null)
    {
    }

    public void SetDefaults(EventSourcingOptionsBuilder builder)
    {
        if (!builder.EventStreamOptionsConfigured())
            new SqliteEventStoreOptionsBuilder(builder)
                .UsePollingEventStream();
    }

    public void ApplyServices(IServiceCollection serviceCollection)
    {
        serviceCollection.AddDbContext<EventStoreContext>((sp, options) =>
            options.UseSqlite(ConnectionString(sp), o => o.MigrationsAssembly(typeof(Marker).Assembly.GetName().Name!))
        );

        serviceCollection.AddEntityFrameworkServices();
    }

    public void AddDefaultServices(IServiceCollection serviceCollection, EventSourcingOptions eventSourcingOptions)
    {
    }
}