using EventSourcing.Infrastructure;
using EventSourcing.Persistence.EntityFramework;
using EventSourcing.Persistence.EntityFramework.SqlServer.Infrastructure;
using AwesomeAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using DbEvent = EventSourcing.Persistence.EntityFramework.Event;

namespace EventSourcing.Test;

[Trait("Category", "SqlServerIntegration")]
public class SqlServerEventStoreTest
{
    static readonly TimeSpan Timeout = TimeSpan.FromSeconds(20);

    [Theory]
    [InlineData(3, 10)]
    [InlineData(1000, 10)]
    [InlineData(3, 800000)]
    public async Task TriggerFailureRollsBackWholeBatchAndSameScopeRecoversAcrossGap(int count, int size)
    {
        await using var database = await TestDatabase.Create();
        using var scope = database.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EventStoreContext>();
        context.Should().BeSameAs(scope.ServiceProvider.GetRequiredService<SqlServerEventStoreContext>());
        context.Database.HasPendingModelChanges().Should().BeFalse();
        await context.Database.ExecuteSqlRawAsync("""
            CREATE TRIGGER dbo.RejectAbandoned ON dbo.Events AFTER INSERT AS
            BEGIN
                IF EXISTS (SELECT 1 FROM inserted WHERE Payload = N'abandoned')
                    THROW 51001, 'Injected append failure', 1;
            END;
            """, TestContext.Current.CancellationToken);
        var writer = scope.ServiceProvider.GetRequiredService<IEventWriter<DbEvent>>();
        var rows = Enumerable.Range(0, count).Select(_ => Raw(new string('界', size))).ToArray();
        rows[^1] = Raw("abandoned");
        Func<Task> append = () => writer.WriteEvents(rows);
        await append.Should().ThrowAsync<SqlException>().WithMessage("*Injected append failure*");
        context.ChangeTracker.Entries().Should().BeEmpty();
        (await context.Events.CountAsync(TestContext.Current.CancellationToken)).Should().Be(0);
        await writer.WriteEvents([Raw("recovered")]);
        var recovered = await context.Events.SingleAsync(TestContext.Current.CancellationToken);
        recovered.Position.Should().Be(count + 1);
        recovered.Payload.Should().Be("recovered");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DoomedTransactionIsNotAcknowledgedAndCanRecover(bool bulk)
    {
        await using var database = await TestDatabase.Create();
        using var scope = database.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EventStoreContext>();
        await context.Database.ExecuteSqlRawAsync("""
            CREATE TRIGGER dbo.DoomTransaction ON dbo.Events AFTER INSERT AS
            BEGIN
                IF EXISTS (SELECT 1 FROM inserted WHERE Payload = N'abandoned')
                BEGIN
                    BEGIN TRY
                        DECLARE @zero int = 0, @result int;
                        SET @result = 1 / @zero;
                    END TRY
                    BEGIN CATCH
                        -- Swallow the statement error; XACT_ABORT leaves commit impossible.
                    END CATCH
                END
            END;
            """, TestContext.Current.CancellationToken);
        var count = bulk ? 1000 : 2;
        var writer = scope.ServiceProvider.GetRequiredService<IEventWriter<DbEvent>>();
        Func<Task> append = () => writer.WriteEvents(Enumerable.Range(0, count).Select(_ => Raw("abandoned")));
        await append.Should().ThrowAsync<SqlException>();
        (await context.Events.CountAsync(TestContext.Current.CancellationToken)).Should().Be(0);
        await writer.WriteEvents([Raw("recovered")]);
        (await context.Events.SingleAsync(TestContext.Current.CancellationToken)).Position.Should().Be(count + 1);
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 1)]
    [InlineData(false, 43)]
    [InlineData(true, 43)]
    [InlineData(false, 1000)]
    [InlineData(true, 1000)]
    public async Task WriterWaitsUntilEarlierBatchCommitsOrRollsBackAndPollingCrossesGaps(bool rollback, int secondCount)
    {
        await using var database = await TestDatabase.Create();
        using var scope = database.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IEventStore>();
        await store.WriteEvents([new SqlServerBatchOrderingTest.OrderedEntry(0, "A", "first")]);
        var context = scope.ServiceProvider.GetRequiredService<EventStoreContext>();
        var row = await context.Events.SingleAsync(TestContext.Current.CancellationToken);
        await context.Database.ExecuteSqlRawAsync("TRUNCATE TABLE dbo.Events;", TestContext.Current.CancellationToken);
        await using var blocker = new SqlConnection(database.ConnectionString);
        await blocker.OpenAsync(TestContext.Current.CancellationToken);
        await using var transaction = (SqlTransaction)await blocker.BeginTransactionAsync(TestContext.Current.CancellationToken);
        await using var command = blocker.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT dbo.Events WITH (TABLOCKX) (StreamType, StreamId, EventType, Payload, Timestamp)
            VALUES (@type, @id, @event, @payload, @timestamp);
            """;
        command.Parameters.AddWithValue("@type", row.StreamType);
        command.Parameters.AddWithValue("@id", row.StreamId);
        command.Parameters.AddWithValue("@event", row.EventType);
        command.Parameters.AddWithValue("@payload", row.Payload);
        command.Parameters.AddWithValue("@timestamp", row.Timestamp);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        using var secondScope = database.Services.CreateScope();
        var second = secondScope.ServiceProvider.GetRequiredService<IEventStore>().WriteEvents(
            Enumerable.Range(1, secondCount).Select(i => new SqlServerBatchOrderingTest.OrderedEntry(i, "B", "second")).ToArray());
        try
        {
            // Observe an actual exclusive Events table-lock waiter, rather than relying on a sleep.
            var deadline = DateTime.UtcNow + Timeout;
            while (await database.Scalar("SELECT COUNT(*) FROM sys.dm_tran_locks WHERE resource_database_id=DB_ID() AND resource_type='OBJECT' AND resource_associated_entity_id=OBJECT_ID('dbo.Events') AND request_mode='X' AND request_status='WAIT'") == 0)
            {
                if (DateTime.UtcNow > deadline) throw new TimeoutException("No append lock waiter observed.");
                await Task.Delay(10, TestContext.Current.CancellationToken);
            }
            second.IsCompleted.Should().BeFalse();
            (await Read(store.ReadEvents())).Should().BeEmpty();
            var delivered = new List<Event>();
            var allDelivered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var subscription = database.Services.GetRequiredService<IObservable<Event>>().Subscribe(e =>
            {
                delivered.Add(e);
                if (delivered.Count == secondCount + (rollback ? 0 : 1)) allDelivered.TrySetResult();
            }, error => allDelivered.TrySetException(error));
            database.Services.GetRequiredService<EventStream<Event>>().Start();
            if (rollback) await transaction.RollbackAsync(TestContext.Current.CancellationToken); else await transaction.CommitAsync(TestContext.Current.CancellationToken);
            await second.WaitAsync(Timeout, TestContext.Current.CancellationToken);
            await allDelivered.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);
            var expected = Enumerable.Range(rollback ? 2 : 1, secondCount + (rollback ? 0 : 1)).Select(i => (long)i).ToArray();
            delivered.Select(e => e.Position).Should().Equal(expected);
            var observed = await Read(store.ReadEvents());
            observed.Select(e => e.Position).Should().Equal(expected);
            var checkpoint = observed[^1].Position;
            await store.WriteEvents([new SqlServerBatchOrderingTest.OrderedEntry(2, "C", "third")]);
            (await Read(store.ReadEvents(checkpoint + 1))).Select(e => e.Position).Should().Equal(secondCount + 2L);
        }
        finally
        {
            if (!second.IsCompleted) await transaction.RollbackAsync(TestContext.Current.CancellationToken);
            await second.WaitAsync(Timeout, TestContext.Current.CancellationToken);
        }
    }

    [Theory]
    [InlineData(false, Skip = "Requires a SQL Server instance configured for Service Broker notifications.")]
    [InlineData(true, Skip = "Requires a SQL Server instance configured for Service Broker notifications.")]
    public async Task BrokerNotifiesAfterCommitForBothTransferPaths(bool bulk)
    {
        await using var database = await TestDatabase.Create();
        await database.Scalar($"ALTER DATABASE [{new SqlConnectionStringBuilder(database.ConnectionString).InitialCatalog}] SET ENABLE_BROKER WITH ROLLBACK IMMEDIATE; SELECT 1;");
        await using var connection = new SqlConnection(database.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        SqlDependency.Start(database.ConnectionString);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT Position, Payload FROM dbo.Events WHERE Position > 0";
            var dependency = new SqlDependency(command);
            var notified = new TaskCompletionSource<SqlNotificationEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
            dependency.OnChange += (_, args) => notified.TrySetResult(args);
            await using (var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken))
                while (await reader.ReadAsync(TestContext.Current.CancellationToken)) { }
            using var scope = database.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IEventWriter<DbEvent>>()
                .WriteEvents(Enumerable.Range(0, bulk ? 1000 : 2).Select(_ => Raw("notification")));
            SqlNotificationEventArgs notification;
            try { notification = await notified.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken); }
            catch (TimeoutException)
            {
                await using var diagnostics = connection.CreateCommand();
                diagnostics.CommandText = "SELECT TOP (1) transmission_status FROM sys.transmission_queue";
                throw new TimeoutException($"Broker delivery failed: {await diagnostics.ExecuteScalarAsync(TestContext.Current.CancellationToken)}");
            }
            notification.Type.Should().Be(SqlNotificationType.Change);
            notification.Info.Should().Be(SqlNotificationInfo.Insert);
            (await database.Scalar("SELECT COUNT(*) FROM dbo.Events")).Should().Be(bulk ? 1000 : 2);
        }
        finally { SqlDependency.Stop(database.ConnectionString); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnicodeLimitsAndUnrestrictedPayloadRoundTripAndInvalidEnumerationWritesNothing(bool bulk)
    {
        await using var database = await TestDatabase.Create();
        using var scope = database.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EventStoreContext>();
        var writer = scope.ServiceProvider.GetRequiredService<IEventWriter<DbEvent>>();
        static string Key(int units) => string.Concat(Enumerable.Repeat("🙂", units / 2));
        var boundary = Raw(bulk ? new string('界', 2200000) + "ä🙂" : "ä🙂界") with
        {
            StreamType = Key(128), StreamId = Key(256), EventType = Key(450),
            Timestamp = new DateTimeOffset(2026, 10, 9, 12, 0, 0, TimeSpan.FromHours(5))
        };
        await writer.WriteEvents([boundary, Raw("")]);
        var persisted = await context.Events.OrderBy(e => e.Position).ToArrayAsync(TestContext.Current.CancellationToken);
        persisted[0].Should().Be(boundary with { Position = 1 });
        persisted[1].Payload.Should().BeEmpty();
        foreach (var invalid in new[] { boundary with { StreamType = Key(128) + "a" }, boundary with { StreamId = Key(256) + "a" }, boundary with { EventType = Key(450) + "a" } })
        {
            Func<Task> append = () => writer.WriteEvents([Raw("partial"), invalid]);
            await append.Should().ThrowAsync<ArgumentException>();
        }
        IEnumerable<DbEvent> Broken()
        {
            yield return Raw("partial");
            throw new InvalidOperationException("Enumeration failed");
        }
        Func<Task> broken = () => writer.WriteEvents(Broken());
        await broken.Should().ThrowAsync<InvalidOperationException>();
        await writer.WriteEvents([]);
        await writer.WriteEvents([Raw("recovered")]);
        (await context.Events.MaxAsync(e => e.Position, TestContext.Current.CancellationToken)).Should().Be(3);
        (await database.Scalar("SELECT COUNT(*) FROM sys.tables WHERE name <> '__EFMigrationsHistory' AND is_ms_shipped=0")).Should().Be(1);
    }

    static DbEvent Raw(string payload) => new(0, "Raw", "A", "Raw.Entry", payload, DateTimeOffset.UtcNow);
    static async Task<List<Event>> Read(IAsyncEnumerable<Event> source)
    {
        var result = new List<Event>();
        await foreach (var e in source) result.Add(e);
        return result;
    }

    sealed class TestDatabase(string connectionString, ServiceProvider services) : IAsyncDisposable
    {
        public string ConnectionString => connectionString;
        public ServiceProvider Services => services;
        public static async Task<TestDatabase> Create()
        {
            var configured = Environment.GetEnvironmentVariable("TEST_SQLSERVER_CONNECTION_STRING");
            if (string.IsNullOrWhiteSpace(configured))
                throw Xunit.Sdk.SkipException.ForSkip("Set TEST_SQLSERVER_CONNECTION_STRING to run live SQL Server tests.");
            var connection = new SqlConnectionStringBuilder(configured) { InitialCatalog = $"EventSourcing_SqlTest_{Guid.NewGuid():N}" };
            var services = new ServiceCollection().AddEventSourcing(b => b.UseSqlServerEventStore(connection.ConnectionString)
                .PayloadAssemblies(typeof(SqlServerEventStoreTest).Assembly)).BuildServiceProvider();
            var database = new TestDatabase(connection.ConnectionString, services);
            try
            {
                using var scope = services.CreateScope();
                await scope.ServiceProvider.GetRequiredService<EventStoreContext>().Database.MigrateAsync(TestContext.Current.CancellationToken);
                return database;
            }
            catch { await database.DisposeAsync(); throw; }
        }
        public async Task<long> Scalar(string sql)
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            return Convert.ToInt64(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken));
        }
        public async ValueTask DisposeAsync()
        {
            await services.DisposeAsync();
            var master = new SqlConnectionStringBuilder(connectionString) { InitialCatalog = "master" };
            await using var connection = new SqlConnection(master.ConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            var name = new SqlConnectionStringBuilder(connectionString).InitialCatalog;
            command.CommandText = $"IF DB_ID('{name}') IS NOT NULL BEGIN ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]; END";
            await command.ExecuteNonQueryAsync();
        }
    }
}
