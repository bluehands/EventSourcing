using EventSourcing.Infrastructure;
using EventSourcing.Infrastructure.Internal;
using EventSourcing.Persistence.EntityFramework.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace EventSourcing.Persistence.EntityFramework.SqlServer.Infrastructure.Internal;

public record SqlServerEventStoreOptionsExtension(string? ConnectionString) : IEventSourcingOptionsExtension
{
    public SqlServerEventStoreOptionsExtension() : this(default(string))
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
        serviceCollection.AddDbContext<EventStoreContext>(options =>
            options.UseSqlServer(ConnectionString, o => o.MigrationsAssembly(typeof(Marker).Assembly.GetName().Name!))
        );

        serviceCollection.AddEntityFrameworkServices();
    }

    public void AddDefaultServices(IServiceCollection serviceCollection, EventSourcingOptions eventSourcingOptions)
    {
    }
}