using EventSourcing.Infrastructure;
using EventSourcing.Persistence.EntityFramework.SqlServer.Infrastructure.Internal;

namespace EventSourcing.Persistence.EntityFramework.SqlServer.Infrastructure;

public class SqlServerEventStoreOptionsBuilder(EventSourcingOptionsBuilder optionsBuilder)
    : EventSourcingOptionsExtensionBuilder<SqlServerEventStoreOptionsBuilder>(optionsBuilder), IAllowPollingEventStreamBuilder
{
    public SqlServerEventStoreOptionsBuilder ConnectionString(string connectionString) =>
        WithOption<Internal.SqlServerEventStoreOptionsExtension>(options => options with
        {
            ConnectionString = connectionString
        });

    public SqlServerEventStoreOptionsBuilder UseBrokerNotificationEventStream(uint maxRowsPerSelect = 10000) =>
        WithOption<SqlDependencyEventStreamOptionsExtension>(e => e with
        {
            MaxRowsPerSelect = maxRowsPerSelect
        });
}
