using EventSourcing.Infrastructure;
using EventSourcing.Persistence.EntityFramework.Sqlite.Infrastructure;

// ReSharper disable once CheckNamespace
namespace EventSourcing;

public static class SqliteEventSourcingOptionsExtensions
{
    public static EventSourcingOptionsBuilder UseSqliteEventStore(this EventSourcingOptionsBuilder optionsBuilder, string connectionString, Action<SqliteEventStoreOptionsBuilder>? sqliteOptionsAction = null)
    {
        var sqlBuilder = new SqliteEventStoreOptionsBuilder(optionsBuilder);
        sqlBuilder.UseConnectionString(connectionString);
        sqliteOptionsAction?.Invoke(sqlBuilder);

        SetDefaultEventStreamIfNotConfigured(optionsBuilder, sqlBuilder);
        return optionsBuilder;
    }

    static void SetDefaultEventStreamIfNotConfigured(EventSourcingOptionsBuilder optionsBuilder, SqliteEventStoreOptionsBuilder sqlExtensionBuilder)
    {
        if (!optionsBuilder.EventStreamOptionsConfigured())
        {
            sqlExtensionBuilder.UsePollingEventStream();
        }
    }
}