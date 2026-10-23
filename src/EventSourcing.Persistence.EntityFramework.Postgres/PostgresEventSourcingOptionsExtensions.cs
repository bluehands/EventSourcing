using EventSourcing.Infrastructure.Internal;
using EventSourcing.Persistence.EntityFramework.Postgres.Infrastructure;

namespace EventSourcing;

public static class PostgresEventSourcingOptionsExtensions
{
    public static EventSourcingOptionsBuilder UsePostgresEventStore(
        this EventSourcingOptionsBuilder optionsBuilder, string connectionString,
        Action<PostgresEventStoreOptionsBuilder>? postgresOptionsAction = null) =>
        optionsBuilder.UsePostgresEventStore(_ => connectionString, postgresOptionsAction);

    public static EventSourcingOptionsBuilder UsePostgresEventStore(
        this EventSourcingOptionsBuilder optionsBuilder, Func<IServiceProvider, string> connectionString,
        Action<PostgresEventStoreOptionsBuilder>? postgresOptionsAction = null)
    {
        var builder = new PostgresEventStoreOptionsBuilder(optionsBuilder).ConnectionString(connectionString);
        postgresOptionsAction?.Invoke(builder);
        if (!optionsBuilder.EventStreamOptionsConfigured())
            builder.UsePollingEventStream();
        return optionsBuilder;
    }
}
