using EventSourcing.Infrastructure;
using EventSourcing.Persistence.EntityFramework.Postgres.Infrastructure.Internal;

namespace EventSourcing.Persistence.EntityFramework.Postgres.Infrastructure;

public class PostgresEventStoreOptionsBuilder(EventSourcingOptionsBuilder optionsBuilder)
    : EventSourcingOptionsExtensionBuilder<PostgresEventStoreOptionsBuilder, PostgresEventStoreOptionsExtension>(optionsBuilder), IAllowPollingEventStreamBuilder
{
    public PostgresEventStoreOptionsBuilder ConnectionString(string connectionString) =>
        ConnectionString(_ => connectionString);

    public PostgresEventStoreOptionsBuilder ConnectionString(Func<IServiceProvider, string> connectionString) =>
        WithOption(options => options with { ConnectionString = connectionString });
}
