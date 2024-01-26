using EventSourcing.Infrastructure;
using EventSourcing.Persistence.EntityFramework.SqlServer.Infrastructure;

// ReSharper disable once CheckNamespace
namespace EventSourcing;

public static class SqlServerEventSourcingOptionsExtensions
{
    public static EventSourcingOptionsBuilder UseSqlServerEventStore(this EventSourcingOptionsBuilder optionsBuilder, string connectionString, Action<SqlServerEventStoreOptionsBuilder>? sqlServerOptionsAction = null)
    {
        var sqlBuilder = new SqlServerEventStoreOptionsBuilder(optionsBuilder)
            .ConnectionString(connectionString);
        sqlServerOptionsAction?.Invoke(sqlBuilder);

        return optionsBuilder;
    }
}