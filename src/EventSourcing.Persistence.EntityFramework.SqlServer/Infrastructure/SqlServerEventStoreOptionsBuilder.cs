using EventSourcing.Infrastructure;
using EventSourcing.Persistence.EntityFramework.SqlServer.Infrastructure.Internal;

namespace EventSourcing.Persistence.EntityFramework.SqlServer.Infrastructure;

public class SqlServerEventStoreOptionsBuilder(EventSourcingOptionsBuilder optionsBuilder)
    : EventSourcingOptionsExtensionBuilder<SqlServerEventStoreOptionsBuilder, SqlServerEventStoreOptionsExtension>(optionsBuilder), IAllowPollingEventStreamBuilder
{
    public SqlServerEventStoreOptionsBuilder ConnectionString(string connectionString) =>
        WithOption(options => options with
        {
            ConnectionString = _ => connectionString
        });
    public SqlServerEventStoreOptionsBuilder ConnectionString(Func<IServiceProvider, string> connectionString) =>
        WithOption(options => options with
        {
            ConnectionString = connectionString
        });

    public SqlServerEventStoreOptionsBuilder UseBrokerNotificationEventStream(Func<Task<long>>? getPositionToStartFrom = null, uint maxRowsPerSelect = 10000) =>
        WithOption<SqlDependencyEventStreamOptionsExtension>(e => e with
        {
            MaxRowsPerSelect = maxRowsPerSelect,
            GetPositionToStartFrom = getPositionToStartFrom
        });
}
