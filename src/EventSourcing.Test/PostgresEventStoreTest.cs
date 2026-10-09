using System.Transactions;
using EventSourcing.Commands;
using EventSourcing.Commands.SerializablePayloads;
using EventSourcing.Infrastructure;
using EventSourcing.Persistence.EntityFramework;
using EventSourcing.Persistence.EntityFramework.Postgres.Infrastructure;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using NpgsqlTypes;
using DbEvent = EventSourcing.Persistence.EntityFramework.Event;

namespace EventSourcing.Test;

[Trait("Category", "PostgresIntegration")]
public class PostgresEventStoreTest
{
    static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);
    const string Allocate = """
        WITH locked AS MATERIALIZED (
            SELECT pg_advisory_xact_lock(19467218, 'public."EventPosition"'::regclass::oid::int)
        ), first AS MATERIALIZED (SELECT nextval('public."EventPosition"') AS position FROM locked)
        SELECT setval('public."EventPosition"', first.position + $1::bigint - 1, true) FROM first
        """;

    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(1000)]
    [InlineData(10000)]
    public async Task BatchPreservesInputOrderAndCompletionMarker(int count)
    {
        await using var database = await TestDatabase.Create();
        using var scope = database.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EventStoreContext>();
        context.Should().BeSameAs(scope.ServiceProvider.GetRequiredService<PostgresEventStoreContext>());
        context.Database.HasPendingModelChanges().Should().BeFalse();
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
        var store = scope.ServiceProvider.GetRequiredService<IEventStore>();
        var commandId = CommandId.NewCommandId();
        await store.WriteEvents([
            .. Enumerable.Range(0, count).Select(i => new Entry(i, $"Stream-{(i * 17) % 7}", new string('x', i % 513))),
            new CommandProcessed<string>(CommandResult<string>.Processed(commandId, FunctionalResult<string>.Ok("Done")))
        ]);
        context.ChangeTracker.Entries().Should().BeEmpty();
        var events = await Read(store.ReadEvents());
        events.Select(e => e.Position).Should().Equal(Enumerable.Range(1, count + 1).Select(i => (long)i));
        events.Take(count).Select(e => ((Entry)e.Payload).Ordinal).Should().Equal(Enumerable.Range(0, count));
        events[^1].Payload.Should().BeOfType<CommandProcessed<string>>().Which.CommandId.Should().Be(commandId);
        events.Should().OnlyContain(e => e.Timestamp.Offset == TimeSpan.Zero);
        var streamEvents = await Read(store.ReadEvents(new StreamId("PostgresTest", "Stream-0"), 1));
        streamEvents.Select(e => ((Entry)e.Payload).Ordinal).Should().Equal(Enumerable.Range(0, count).Where(i => (i * 17) % 7 == 0));
        (await Read(store.ReadEvents(count + 1))).Should().ContainSingle();
        await store.WriteEvents([]);
        (await database.Position()).Should().Be(count + 1);
        (await database.Scalar("SELECT count(*) FROM pg_tables WHERE schemaname = 'public' AND tablename <> '__EFMigrationsHistory'"))
            .Should().Be(1);
        (await database.Scalar("SELECT cache_size FROM pg_sequences WHERE schemaname = 'public' AND sequencename = 'EventPosition'"))
            .Should().Be(1);
    }

    [Theory]
    [InlineData(3, 10)] // Arrays.
    [InlineData(1024, 10)] // Count-based COPY.
    [InlineData(3, 400000)] // Byte-based COPY with a small event count.
    public async Task InvalidFinalRowRollsBackWholeBatchAndRecoversAcrossGap(int count, int bytes)
    {
        await using var database = await TestDatabase.Create();
        using var scope = database.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PostgresEventStoreContext>();
        var writer = scope.ServiceProvider.GetRequiredService<IEventWriter<DbEvent>>();
        var batch = Enumerable.Range(0, count).Select(i => Raw(new string('x', bytes))).ToArray();
        batch[^1] = batch[^1] with { Payload = null! };
        Func<Task> fail = () => writer.WriteEvents(batch);
        await fail.Should().ThrowAsync<PostgresException>();
        context.ChangeTracker.Entries().Should().BeEmpty();
        (await context.Events.CountAsync(TestContext.Current.CancellationToken)).Should().Be(0);
        (await database.Position()).Should().Be(count);
        await writer.WriteEvents([Raw("recovered")]);
        var persisted = await context.Events.SingleAsync(TestContext.Current.CancellationToken);
        persisted.Position.Should().Be(count + 1);
        persisted.Payload.Should().Be("recovered");
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task CompetingWriterWaitsThroughCommitOrRollbackAndPollingLosesNoEvents(bool rollback, bool copy)
    {
        await using var database = await TestDatabase.Create();
        using var scope = database.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PostgresEventStoreContext>();
        var store = scope.ServiceProvider.GetRequiredService<IEventStore>();
        // Obtain the provider's actual serialized rows, then hold them uncommitted using its lock protocol.
        await store.WriteEvents([new Entry(0, "A", "first"), new Entry(1, "A", "second")]);
        var rows = await context.Events.AsNoTracking().OrderBy(e => e.Position).ToArrayAsync(TestContext.Current.CancellationToken);
        await context.Database.ExecuteSqlRawAsync("TRUNCATE public.\"Events\"; ALTER SEQUENCE public.\"EventPosition\" RESTART WITH 1;", TestContext.Current.CancellationToken);
        await using var blocker = new NpgsqlConnection(context.Database.GetConnectionString());
        await blocker.OpenAsync(TestContext.Current.CancellationToken);
        await using var transaction = await blocker.BeginTransactionAsync(TestContext.Current.CancellationToken);
        await using var allocate = new NpgsqlCommand(Allocate, blocker);
        allocate.Parameters.Add(new NpgsqlParameter<long> { TypedValue = 2 });
        (await allocate.ExecuteScalarAsync(TestContext.Current.CancellationToken)).Should().Be(2L);
        foreach (var row in rows)
        {
            await using var insert = new NpgsqlCommand("INSERT INTO public.\"Events\" VALUES ($1, $2, $3, $4, $5, $6)", blocker);
            insert.Parameters.Add(new NpgsqlParameter<long> { TypedValue = row.Position });
            foreach (var text in new[] { row.StreamType, row.StreamId, row.EventType, row.Payload })
                insert.Parameters.Add(new NpgsqlParameter<string> { TypedValue = text });
            insert.Parameters.Add(new NpgsqlParameter<DateTime> { TypedValue = row.Timestamp.UtcDateTime, NpgsqlDbType = NpgsqlDbType.TimestampTz });
            await insert.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }
        using var competingScope = database.Services.CreateScope();
        var second = competingScope.ServiceProvider.GetRequiredService<IEventStore>().WriteEvents([
            new Entry(2, "B", copy ? new string('界', 400000) : "third"), new Entry(3, "B", "fourth")]);
        try
        {
            await database.WaitForAppendLock();
            second.IsCompleted.Should().BeFalse();
            (await Read(store.ReadEvents()).WaitAsync(Timeout, TestContext.Current.CancellationToken)).Should().BeEmpty();
            var delivered = new List<Event>();
            var allDelivered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var expected = rollback ? new[] { 2, 3 } : new[] { 0, 1, 2, 3 };
            using var subscription = database.Services.GetRequiredService<IObservable<Event>>().Subscribe(e =>
            {
                delivered.Add(e);
                if (delivered.Count == expected.Length) allDelivered.TrySetResult();
            }, error => allDelivered.TrySetException(error));
            database.Services.GetRequiredService<EventStream<Event>>().Start();
            if (rollback) await transaction.RollbackAsync(TestContext.Current.CancellationToken); else await transaction.CommitAsync(TestContext.Current.CancellationToken);
            await second.WaitAsync(Timeout, TestContext.Current.CancellationToken);
            await allDelivered.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);
            delivered.Select(e => ((Entry)e.Payload).Ordinal).Should().Equal(expected);
            delivered.Select(e => e.Position).Should().Equal(rollback ? new long[] { 3, 4 } : [1L, 2L, 3L, 4L]);
            (await database.Position()).Should().Be(4);
        }
        finally
        {
            if (!second.IsCompleted) await transaction.RollbackAsync(TestContext.Current.CancellationToken);
            await second.WaitAsync(Timeout, TestContext.Current.CancellationToken);
        }
    }

    [Theory]
    [InlineData(2)]
    [InlineData(1024)]
    public async Task DeferredFailureAtCommitRollsBackAndAllowsSameScopeRecovery(int count)
    {
        await using var database = await TestDatabase.Create();
        using var scope = database.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PostgresEventStoreContext>();
        await context.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION public.reject_abandoned() RETURNS trigger LANGUAGE plpgsql AS $body$
            BEGIN
                IF NEW."Payload" = 'abandoned' THEN RAISE EXCEPTION 'Injected commit failure'; END IF;
                RETURN NEW;
            END $body$;
            CREATE CONSTRAINT TRIGGER reject_abandoned AFTER INSERT ON public."Events"
                DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION public.reject_abandoned();
            """, TestContext.Current.CancellationToken);
        var writer = scope.ServiceProvider.GetRequiredService<IEventWriter<DbEvent>>();
        Func<Task> append = () => writer.WriteEvents(Enumerable.Range(0, count).Select(_ => Raw("abandoned")));
        await append.Should().ThrowAsync<PostgresException>().WithMessage("*Injected commit failure*");
        context.ChangeTracker.Entries().Should().BeEmpty();
        (await context.Events.CountAsync(TestContext.Current.CancellationToken)).Should().Be(0);
        await writer.WriteEvents([Raw("recovered")]);
        var recovered = await context.Events.SingleAsync(TestContext.Current.CancellationToken);
        recovered.Position.Should().Be(count + 1);
        recovered.Payload.Should().Be("recovered");
    }

    [Fact]
    public async Task RejectsExternalTransactionsExplicitPositionsAndUnrelatedPendingChanges()
    {
        await using var database = await TestDatabase.Create();
        using var scope = database.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EventStoreContext>();
        var writer = scope.ServiceProvider.GetRequiredService<IEventWriter<DbEvent>>();
        await using (var transaction = await context.Database.BeginTransactionAsync(TestContext.Current.CancellationToken))
        {
            Func<Task> append = () => writer.WriteEvents([Raw("external")]);
            await append.Should().ThrowAsync<InvalidOperationException>().WithMessage("*provider-owned*");
        }
        using (var ambient = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
        {
            Func<Task> append = () => writer.WriteEvents([Raw("ambient")]);
            await append.Should().ThrowAsync<InvalidOperationException>().WithMessage("*provider-owned*");
        }
        Func<Task> explicitPosition = () => writer.WriteEvents([Raw("explicit") with { Position = 99 }]);
        await explicitPosition.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Position = 0*");
        var pending = Raw("pending") with { Position = 123 };
        context.Events.Add(pending);
        Func<Task> unrelated = () => writer.WriteEvents([Raw("append")]);
        await unrelated.Should().ThrowAsync<InvalidOperationException>().WithMessage("*pending changes*");
        context.Entry(pending).State.Should().Be(EntityState.Added);
        (await database.Position()).Should().Be(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnevenUnicodePayloadsAndUtcTimestampsRoundTripAndEnumerationFailureAllocatesNothing(bool copy)
    {
        await using var database = await TestDatabase.Create();
        using var scope = database.Services.CreateScope();
        var writer = scope.ServiceProvider.GetRequiredService<IEventWriter<DbEvent>>();
        var timestamp = new DateTimeOffset(2026, 10, 9, 12, 30, 0, TimeSpan.FromHours(5));
        var payloads = new[] { "ä🙂", copy ? new string('界', 400000) : "界", "" };
        await writer.WriteEvents(payloads.Select(text => Raw(text) with { Timestamp = timestamp }));
        var persisted = await scope.ServiceProvider.GetRequiredService<EventStoreContext>().Events.OrderBy(e => e.Position).ToArrayAsync(TestContext.Current.CancellationToken);
        persisted.Select(e => e.Payload).Should().Equal(payloads);
        persisted.Should().OnlyContain(e => e.Timestamp == timestamp.ToUniversalTime() && e.Timestamp.Offset == TimeSpan.Zero);
        IEnumerable<DbEvent> BrokenBatch()
        {
            yield return Raw("partial enumeration");
            throw new InvalidOperationException("Enumeration failed");
        }
        Func<Task> append = () => writer.WriteEvents(BrokenBatch());
        await append.Should().ThrowAsync<InvalidOperationException>().WithMessage("Enumeration failed");
        (await database.Position()).Should().Be(3);
    }

    static DbEvent Raw(string payload) => new(0, "Raw", "A", "Raw.Entry", payload, DateTimeOffset.UtcNow);

    [Theory]
    [InlineData(false, "StreamType", 128)]
    [InlineData(true, "StreamType", 128)]
    [InlineData(false, "StreamId", 256)]
    [InlineData(true, "StreamId", 256)]
    [InlineData(false, "EventType", 450)]
    [InlineData(true, "EventType", 450)]
    public async Task IdentifierLimitsPreserveUnicodeBoundaryAndRejectWholeOverlengthBatch(bool copy, string column, int limit)
    {
        await using var database = await TestDatabase.Create();
        using var scope = database.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PostgresEventStoreContext>();
        context.Database.HasPendingModelChanges().Should().BeFalse();
        var writer = scope.ServiceProvider.GetRequiredService<IEventWriter<DbEvent>>();
        // Four-byte Unicode characters count as one PostgreSQL character each. Both indexed keys
        // reach their limits together, exercising the worst-case encoded composite-key size.
        static string Key(int length) => string.Concat(Enumerable.Repeat("🙂", length));
        var boundary = Raw(copy ? new string('x', 1100000) : "boundary") with
        {
            StreamType = Key(128), StreamId = Key(256), EventType = Key(450)
        };
        await writer.WriteEvents([boundary]);
        var persisted = await context.Events.SingleAsync(TestContext.Current.CancellationToken);
        persisted.StreamType.Should().Be(boundary.StreamType);
        persisted.StreamId.Should().Be(boundary.StreamId);
        persisted.EventType.Should().Be(boundary.EventType);
        var invalid = column switch
        {
            "StreamType" => boundary with { StreamType = Key(limit + 1) },
            "StreamId" => boundary with { StreamId = Key(limit + 1) },
            _ => boundary with { EventType = Key(limit + 1) }
        };
        Func<Task> append = () => writer.WriteEvents([Raw("must roll back"), invalid]);
        await append.Should().ThrowAsync<PostgresException>().Where(e => e.SqlState == PostgresErrorCodes.StringDataRightTruncation);
        (await context.Events.CountAsync(TestContext.Current.CancellationToken)).Should().Be(1);
        await writer.WriteEvents([Raw("recovered")]);
        (await context.Events.OrderBy(e => e.Position).LastAsync(TestContext.Current.CancellationToken)).Payload.Should().Be("recovered");
    }
    static async Task<List<Event>> Read(IAsyncEnumerable<Event> source)
    {
        var events = new List<Event>();
        await foreach (var e in source) events.Add(e);
        return events;
    }

    [SerializableEventPayload("PostgresTest.Entry")]
    public record Entry(int Ordinal, string JournalId, string Text)
        : EventPayload(new StreamId("PostgresTest", JournalId), "PostgresTest.Entry");

    sealed class TestDatabase(string connectionString, ServiceProvider services) : IAsyncDisposable
    {
        public ServiceProvider Services => services;
        public static async Task<TestDatabase> Create()
        {
            var configured = Environment.GetEnvironmentVariable("TEST_POSTGRES_CONNECTION_STRING");
            if (string.IsNullOrWhiteSpace(configured))
            {
                if (Environment.GetEnvironmentVariable("TEST_POSTGRES_REQUIRED") == "true")
                    throw new InvalidOperationException("PostgreSQL integration tests are required, but TEST_POSTGRES_CONNECTION_STRING is missing.");
                throw Xunit.Sdk.SkipException.ForSkip("Set TEST_POSTGRES_CONNECTION_STRING to run PostgreSQL integration tests.");
            }
            var connection = new NpgsqlConnectionStringBuilder(configured)
            {
                Database = $"eventsourcing_test_{Guid.NewGuid():N}", ApplicationName = $"eventsourcing_test_{Guid.NewGuid():N}"
            };
            var services = new ServiceCollection()
                .AddEventSourcing(options => options.UsePostgresEventStore(_ => connection.ConnectionString)
                    .PayloadAssemblies(typeof(PostgresEventStoreTest).Assembly))
                .AddSingleton<EventPayloadMapper>(new CommandProcessedMapper<string, FailedSaveIsolationTest.ErrorPayload>())
                .BuildServiceProvider();
            var database = new TestDatabase(connection.ConnectionString, services);
            try
            {
                using var scope = services.CreateScope();
                await scope.ServiceProvider.GetRequiredService<EventStoreContext>().Database.MigrateAsync();
                return database;
            }
            catch { await database.DisposeAsync(); throw; }
        }

        public Task<long> Position() => Scalar("SELECT CASE WHEN is_called THEN last_value ELSE 0 END FROM public.\"EventPosition\"");
        public async Task<long> Scalar(string sql)
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(sql, connection);
            return Convert.ToInt64(await command.ExecuteScalarAsync());
        }
        public async Task WaitForAppendLock()
        {
            using var cancellation = new CancellationTokenSource(Timeout);
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(cancellation.Token);
            await using var command = new NpgsqlCommand("""
                SELECT EXISTS (SELECT 1 FROM pg_stat_activity
                    WHERE datname = current_database() AND pid <> pg_backend_pid()
                      AND wait_event_type = 'Lock' AND query LIKE '%pg_advisory_xact_lock%')
                """, connection);
            while (!(bool)(await command.ExecuteScalarAsync(cancellation.Token))!)
                await Task.Delay(20, cancellation.Token);
        }
        public async ValueTask DisposeAsync()
        {
            await services.DisposeAsync();
            await using var context = new PostgresEventStoreContext(new DbContextOptionsBuilder<PostgresEventStoreContext>().UseNpgsql(connectionString).Options);
            await context.Database.EnsureDeletedAsync();
        }
    }
}
