using EventSourcing.Infrastructure;
using EventSourcing.Persistence.EntityFramework.Sqlite.Infrastructure;

// ReSharper disable once CheckNamespace
namespace EventSourcing;

public static class SqliteEventSourcingOptionsExtensions
{
    public static EventSourcingOptionsBuilder UseSqliteEventStore(this EventSourcingOptionsBuilder optionsBuilder, string connectionString, Action<SqliteEventStoreOptionsBuilder>? sqliteOptionsAction = null)
    {
        var sqlBuilder = new SqliteEventStoreOptionsBuilder(optionsBuilder)
            .ConnectionString(connectionString);
        
        sqliteOptionsAction?.Invoke(sqlBuilder);

        return optionsBuilder
            .SetDefaultEventStreamIfNotConfigured(sqlBuilder);
    }

    static EventSourcingOptionsBuilder SetDefaultEventStreamIfNotConfigured(this EventSourcingOptionsBuilder optionsBuilder, SqliteEventStoreOptionsBuilder sqlExtensionBuilder)
    {
        if (!optionsBuilder.EventStreamOptionsConfigured())
        {
            sqlExtensionBuilder.UsePollingEventStream();
        }

        return optionsBuilder;
    }

    public static EventSourcingOptionsBuilder UseInMemoryEventStore(this EventSourcingOptionsBuilder optionsBuilder, Action<InMemoryEventStoreOptionsBuilder>? inMemoryOptionsAction = null, string? dbName = null)
    {
        var builder = new InMemoryEventStoreOptionsBuilder(optionsBuilder)
            .DatabaseName(dbName ?? $"inMemoryDb_{Guid.NewGuid():N}");

        builder.UsePollingEventStream(TimeSpan.FromMilliseconds(0), TimeSpan.FromMilliseconds(50));

        inMemoryOptionsAction?.Invoke(builder);
        
        return optionsBuilder;
    }
}