using EventSourcing.Infrastructure;
using EventSourcing.Infrastructure.Internal;
using EventSourcing.Persistence.EntityFramework.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EventSourcing.Persistence.EntityFramework.Sqlite.Infrastructure.Internal;

public record SqliteEventStoreOptionsExtension(string? ConnectionString) : IEventSourcingOptionsExtension
{
    public SqliteEventStoreOptionsExtension() : this(default(string))
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
        serviceCollection.AddDbContext<EventStoreContext>(options =>
            options.UseSqlite(ConnectionString, o => o.MigrationsAssembly(typeof(Marker).Assembly.GetName().Name!))
        );

        serviceCollection.AddEntityFrameworkServices();
    }

    public void AddDefaultServices(IServiceCollection serviceCollection, EventSourcingOptions eventSourcingOptions)
    {
    }
}