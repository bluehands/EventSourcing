using EventSourcing.Infrastructure;
using EventSourcing.Persistence.EntityFramework.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EventSourcing.Persistence.EntityFramework.Sqlite.Infrastructure.Internal;

public record SqliteEventStoreOptionsExtension(string? ConnectionString) : IEventSourcingOptionsExtension
{
    public SqliteEventStoreOptionsExtension() : this(default(string))
    {
    }

    public void ApplyServices(IServiceCollection serviceCollection)
    {
        serviceCollection.AddDbContext<EventStoreContext>(options =>
            options.UseSqlite(ConnectionString, o => o.MigrationsAssembly(typeof(Marker).Assembly.GetName().Name!))
        );

        serviceCollection.AddEntityFrameworkServices();
    }
}