using EventSourcing.Infrastructure;
using EventSourcing.Persistence.EntityFramework.Sqlite.Infrastructure.Internal;
using Microsoft.Data.Sqlite;

namespace EventSourcing.Persistence.EntityFramework.Sqlite.Infrastructure;

public class InMemoryEventStoreOptionsBuilder(EventSourcingOptionsBuilder optionsBuilder)
    : EventSourcingOptionsExtensionBuilder<InMemoryEventStoreOptionsBuilder, InMemoryEventStoreOptionsExtension>(optionsBuilder), IAllowPollingEventStreamBuilder
{
    public InMemoryEventStoreOptionsBuilder DatabaseName(string databaseName)
    {
        var connectionString = $"DataSource={databaseName};mode=memory;cache=shared";
        OptionsBuilder.UseSqliteEventStore(connectionString);

        return WithOption(e =>
        {
            var keepAliveConnection = new SqliteConnection(connectionString);
            keepAliveConnection.Open();
            return e with { KeepAliveConnection = keepAliveConnection };
        });
    }
}