using EventSourcing.Infrastructure;
using EventSourcing.Persistence.EntityFramework.Sqlite.Infrastructure.Internal;

// ReSharper disable once CheckNamespace
namespace EventSourcing.Persistence.EntityFramework.Sqlite.Infrastructure;

public class SqliteEventStoreOptionsBuilder(EventSourcingOptionsBuilder optionsBuilder)
    : EventSourcingOptionsExtensionBuilder<SqliteEventStoreOptionsBuilder, SqliteEventStoreOptionsExtension>(optionsBuilder), IAllowPollingEventStreamBuilder
{
    public SqliteEventStoreOptionsBuilder ConnectionString(string connectionString) =>
        WithOption(options => options with
        {
            ConnectionString = connectionString
        });
}