using System.Data;
using System.Reactive;
using System.Reactive.Linq;
using EventSourcing.Infrastructure;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EventSourcing.Persistence.EntityFramework.SqlServer.Infrastructure;

static class SqlStatements
{
    public static string AssertBrokerFeatureEnabled(string databaseName) =>
        $"""
         DECLARE @DBName sysname;
         SET @DBName = '{databaseName}'
         
         IF NOT EXISTS (Select is_broker_enabled from sys.databases WHERE name = @DBName and is_broker_enabled = 1)
         BEGIN
           DECLARE @SQL varchar(1000);
           SET @SQL = 'ALTER DATABASE ['+@DBName+'] SET ENABLE_BROKER WITH ROLLBACK IMMEDIATE;'
           print(@sql)
           exec(@sql)
         END
         """;

    public const string SelectQuery =
        $"""
         SELECT
         	[{nameof(Event.Position)}],
         	[{nameof(Event.EventType)}],
         	[{nameof(Event.StreamId)}],
         	[{nameof(Event.StreamType)}],
         	[{nameof(Event.Timestamp)}],
         	[{nameof(Event.Payload)}]
         FROM dbo.Events
         WHERE {nameof(Event.Position)} > @{nameof(Event.Position)}
         ORDER BY {nameof(Event.Position)}
        """;
}

static class BrokerNotificationEventStream
{
    class SqlServerExecutor : IDbExecutor
    {
        readonly IServiceProvider _serviceProvider;

        public SqlServerExecutor(IServiceProvider serviceProvider) => _serviceProvider = serviceProvider;

        public async Task<T> Execute<T>(Func<SqlConnection, Task<T>> executeWithConnection)
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<EventStoreContext>();
            return await executeWithConnection((SqlConnection)dbContext.Database.GetDbConnection());
        }
    }

    public static void AddEventStream(IServiceCollection services, uint maxRowsPerSelect, Func<Task<long>> getPositionToStartFrom)
    {
        services.AddSingleton(sp =>
        {
            var scope = sp.CreateScope();
            var eventMapper = scope.ServiceProvider.GetRequiredService<IEventMapper<Event>>();
            var logger = scope.ServiceProvider.GetService<ILogger<SqlDependencyChangeListener>>();

            //TODO: load existing in buffers, because right now all events are read at once when stream starts
            var innerStream = SqlDependencyChangeListener
                .GetChangeStream(new SqlServerExecutor(sp), (lastPosition, connection) =>
                    {
                        var cmd = connection.CreateCommand();
                        cmd.CommandText = SqlStatements.SelectQuery;
                        cmd.Parameters.Add(new($"@{nameof(Event.Position)}", SqlDbType.BigInt)
                        {
                            Value = lastPosition
                        });
                        return cmd;
                    },
                    async (reader, state, _) =>
                    {
                        var dbEvents = ReadEvents(reader);
                        var result = await eventMapper.MapFromDbEvents(dbEvents).ToListAsync();
                        var nextState = result.Count > 0 ? result[^1].Position : state;
                        return (result, nextState);
                    }, getPositionToStartFrom,
                    logger)
                .SelectMany(l => l);

            return new EventStream<EventSourcing.Event>(innerStream, scope);
        });
    }

    static async IAsyncEnumerable<Event> ReadEvents(SqlDataReader reader)
    {
        var ordinal = GetOrdinal(reader);
        while (await reader.ReadAsync())
        {
            var @event = Read(reader, ordinal);
            yield return @event;
        }
    }

    static Event Read(SqlDataReader reader, EventOrdinal ordinal) =>
        new(
            reader.GetInt64(ordinal.Position),
            reader.GetString(ordinal.StreamType),
            reader.GetString(ordinal.StreamId),
            reader.GetString(ordinal.EventType),
            reader.GetString(ordinal.Payload),
            reader.GetDateTimeOffset(ordinal.Timestamp)
        );

    static EventOrdinal GetOrdinal(SqlDataReader reader) =>
        new(reader.GetOrdinal(nameof(Event.Position)),
            reader.GetOrdinal(nameof(Event.EventType)),
            reader.GetOrdinal(nameof(Event.StreamType)),
            reader.GetOrdinal(nameof(Event.StreamId)),
            reader.GetOrdinal(nameof(Event.Timestamp)),
            reader.GetOrdinal(nameof(Event.Payload))
        );

    record struct EventOrdinal(int Position, int EventType, int StreamType, int StreamId, int Timestamp, int Payload);
}

interface IDbExecutor
{
    Task<T> Execute<T>(Func<SqlConnection, Task<T>> executeWithConnection);
}

class SqlDependencyChangeListener
{
    public static IObservable<T> GetChangeStream<T, TState>(
        IDbExecutor dbExecutor,
        Func<TState, SqlConnection, SqlCommand> createCommand,
        Func<SqlDataReader, TState, SqlNotificationInfo?, Task<(T result, TState nextState)>> read,
        Func<Task<TState>> initialState,
        ILogger<SqlDependencyChangeListener>? logger) =>
        Observable.Create<T>(async (observer, cancellationToken) =>
        {
            await dbExecutor.Execute(async connection =>
            {
                var assertBrokerFeatureEnabledSql = SqlStatements.AssertBrokerFeatureEnabled(connection.Database);
                try
                {
                    if (logger?.IsEnabled(LogLevel.Debug) ?? false) logger.LogDebug("Executing command to make sure broker feature enabled: {EnableBrokerSql}", assertBrokerFeatureEnabledSql);
                    await connection.OpenAsync(cancellationToken);
                    await using var command = connection.CreateCommand();
                    command.CommandText = assertBrokerFeatureEnabledSql;
                    await command.ExecuteNonQueryAsync(cancellationToken);
                }
                catch (Exception e)
                {
                    logger?.LogError(e, "Failed to enable SQL Server broker feature. Try to enable manually: {EnableBrokerSql}", assertBrokerFeatureEnabledSql);
                    throw;
                }

                //TODO: call stop when disposed????
                SqlDependency.Start(connection.ConnectionString);
                return Unit.Default;
            });

            var listener = new SqlDependencyListener<T, TState>(dbExecutor, initialState, createCommand, read, observer, cancellationToken, logger);
            await listener.Run();
        });

    class SqlDependencyListener<T, TState>
    {
        readonly IDbExecutor _dbExecutor;
        readonly Func<Task<TState>> _initialState;
        readonly Func<TState, SqlConnection, SqlCommand> _createCommand;
        readonly Func<SqlDataReader, TState, SqlNotificationInfo?, Task<(T result, TState nextState)>> _read;
        readonly IObserver<T> _observer;
        readonly CancellationToken _cancellationToken;
        TState _currentState = default!;
        readonly ILogger<SqlDependencyChangeListener>? _log;

        public SqlDependencyListener(
            IDbExecutor dbExecutor,
            Func<Task<TState>> initialState,
            Func<TState, SqlConnection, SqlCommand> createCommand,
            Func<SqlDataReader, TState, SqlNotificationInfo?, Task<(T result, TState nextState)>> read,
            IObserver<T> observer,
            CancellationToken cancellationToken,
            ILogger<SqlDependencyChangeListener>? log)
        {
            _dbExecutor = dbExecutor;
            _initialState = initialState;
            _createCommand = createCommand;
            _read = read;
            _observer = observer;
            _cancellationToken = cancellationToken;
            _log = log;
            
        }

        public async Task Run()
        {
            SqlNotificationInfo? notificationInfo = null;
            _currentState = await _initialState();
            while (!_cancellationToken.IsCancellationRequested)
            {
                try
                {
                    notificationInfo = await ReloadData(notificationInfo);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception? e)
                {
                    _log?.LogWarning(e, "SqlDependency query failed, retrying in 5 seconds");
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(5), _cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }
        }

        Task<SqlNotificationInfo> ReloadData(SqlNotificationInfo? changeInfo)
        {
            return _dbExecutor.Execute(async connection =>
            {
                await connection.OpenAsync(_cancellationToken);

                await using var command = _createCommand(_currentState, connection);
                var dependency = new SqlDependency(command);
                var tcs = new TaskCompletionSource<SqlNotificationInfo>(TaskCreationOptions.RunContinuationsAsynchronously);
                dependency.OnChange += (_, e) =>
                {
                    if (e.Info == SqlNotificationInfo.Invalid)
                        _log?.LogError("Sql dependency error: {SqlNotificationEventType}, {SqlNotificationEventInfo}, {SqlNotificationEventSource}. Sql command might not be valid for sql dependency.", e.Type, e.Info, e.Source);

                    _log?.LogDebug("Sql dependency change received: {SqlNotificationEventType}, {SqlNotificationEventInfo}, {SqlNotificationEventSource}", e.Type, e.Info, e.Source);
                    tcs.TrySetResult(e.Info);
                };

                await using (var reader = await command.ExecuteReaderAsync(_cancellationToken))
                {
                    var (result, nextState) = await _read(reader, _currentState, changeInfo);
                    _currentState = nextState;
                    _observer.OnNext(result);
                }

                return await tcs.Task.WaitAsync(_cancellationToken);
            });
        }
    }
}