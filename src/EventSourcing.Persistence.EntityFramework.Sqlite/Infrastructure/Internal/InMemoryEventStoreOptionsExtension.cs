using System.Data;
using EventSourcing.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace EventSourcing.Persistence.EntityFramework.Sqlite.Infrastructure.Internal;

public record InMemoryEventStoreOptionsExtension(SqliteConnection? KeepAliveConnection) : IEventSourcingOptionsExtension
{
    public InMemoryEventStoreOptionsExtension() : this(default(SqliteConnection))
    {
    }

    public void ApplyServices(IServiceCollection serviceCollection)
    {
        if (KeepAliveConnection == null || KeepAliveConnection.State != ConnectionState.Open)
            throw new InvalidOperationException("Sqlite keep alive connection has to be set and open");

        serviceCollection.AddSingleton(KeepAliveConnection!);
    }
}