using System.Data.Common;
using System.Transactions;
using EventSourcing.Commands;
using EventSourcing.Commands.SerializablePayloads;
using EventSourcing.Infrastructure;
using EventSourcing.Persistence.EntityFramework;
using EventSourcing.Persistence.EntityFramework.Postgres.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using DbEvent = EventSourcing.Persistence.EntityFramework.Event;

namespace EventSourcing.Test;

[TestClass]
[TestCategory("PostgresIntegration")]
public class PostgresEventStoreTest
{
    static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    [TestMethod]
    [DataRow(1)]
    [DataRow(50)]
    [DataRow(1000)]
    public async Task BatchPreservesInputOrderAndCompletionMarkerWithOneSave(int count)
    {
        var saves = new CountSaves();
        await using var database = await TestDatabase.Create(saves);
        using var scope = database.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EventStoreContext>();
        context.Should().BeSameAs(scope.ServiceProvider.GetRequiredService<PostgresEventStoreContext>());
        context.Database.HasPendingModelChanges().Should().BeFalse();
        await context.Database.MigrateAsync(); // Repeated startup is harmless.
        var store = scope.ServiceProvider.GetRequiredService<IEventStore>();
        var commandId = CommandId.NewCommandId();
        await store.WriteEvents([
            .. Enumerable.Range(0, count).Select(i => new Entry(i, $"Stream-{(i * 17) % 7}", new string('x', i % 513))),
            new CommandProcessed<string>(CommandResult<string>.Processed(commandId, FunctionalResult<string>.Ok("Done")))
        ]);
        saves.Attempts.Should().Be(1);
        context.ChangeTracker.Entries().Should().BeEmpty();

        var events = await Read(store.ReadEvents());
        events.Select(e => e.Position).Should().Equal(Enumerable.Range(1, count + 1).Select(i => (long)i));
        events.Take(count).Select(e => ((Entry)e.Payload).Ordinal).Should().Equal(Enumerable.Range(0, count));
        events[^1].Payload.Should().BeOfType<CommandProcessed<string>>().Which.CommandId.Should().Be(commandId);
        events.Should().OnlyContain(e => e.Timestamp.Offset == TimeSpan.Zero);

        var streamEvents = await Read(store.ReadEvents(new StreamId("PostgresTest", "Stream-0"), 1));
        streamEvents.Select(e => ((Entry)e.Payload).Ordinal).Should().Equal(
            Enumerable.Range(0, count).Where(i => (i * 17) % 7 == 0));
        (await Read(store.ReadEvents(count + 1))).Should().ContainSingle();

        await store.WriteEvents([]);
        saves.Attempts.Should().Be(1);
        (await database.Counter()).Should().Be(count + 1);
    }

    [TestMethod]
    public async Task FailedLaterInsertRollsBackCounterAndBatchAndAllowsSameScopeRecovery()
    {
        var capture = new CaptureInserts();
        await using var database = await TestDatabase.Create(capture, maxBatchSize: 1);
        using var scope = database.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PostgresEventStoreContext>();
        // A one-command batch size makes a valid insert reach PostgreSQL before the invalid one.
        var writer = scope.ServiceProvider.GetRequiredService<IEventWriter<DbEvent>>();
        Func<Task> fail = () => writer.WriteEvents([
            Raw("valid"), Raw("invalid") with { Payload = null! }, Raw("never committed")]);
        await fail.Should().ThrowAsync<DbUpdateException>();
        capture.ExecutedInserts.Should().BeGreaterThan(0);
        context.ChangeTracker.Entries().Should().BeEmpty();
        (await database.Counter()).Should().Be(0);
        (await context.Events.CountAsync()).Should().Be(0);

        await writer.WriteEvents([Raw("recovered")]);
        var persisted = await context.Events.SingleAsync();
        persisted.Position.Should().Be(1);
        persisted.Payload.Should().Be("recovered");
    }

    [TestMethod]
    public async Task FailureAfterSaveBeforeCommitDetachesUnchangedEventsAndRollsBack()
    {
        var fail = new FailAfterSave();
        await using var database = await TestDatabase.Create(fail);
        using var scope = database.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EventStoreContext>();
        var writer = scope.ServiceProvider.GetRequiredService<IEventWriter<DbEvent>>();
        Func<Task> append = () => writer.WriteEvents([Raw("abandoned")]);
        await append.Should().ThrowAsync<InvalidOperationException>().WithMessage("Injected after-save failure");
        context.ChangeTracker.Entries().Should().BeEmpty();
        (await database.Counter()).Should().Be(0);
        await writer.WriteEvents([Raw("recovered")]);
        (await context.Events.SingleAsync()).Payload.Should().Be("recovered");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task CompetingWriterWaitsThroughCommitOrRollbackAndPollingLosesNoEvents(bool rollback)
    {
        var gate = new HoldFirstSave(rollback);
        await using var database = await TestDatabase.Create(gate);
        using var scopeA = database.Services.CreateScope();
        using var scopeB = database.Services.CreateScope();
        var first = scopeA.ServiceProvider.GetRequiredService<IEventStore>()
            .WriteEvents([new Entry(0, "A", "first"), new Entry(1, "A", "second")]);
        Task? second = null;
        try
        {
            await gate.Saved.Task.WaitAsync(Timeout);
            second = scopeB.ServiceProvider.GetRequiredService<IEventStore>()
                .WriteEvents([new Entry(2, "B", "third"), new Entry(3, "B", "fourth")]);
            await database.WaitForCounterLock();
            second.IsCompleted.Should().BeFalse();

            // Reads remain unblocked and see no uncommitted batch.
            using var readScope = database.Services.CreateScope();
            var store = readScope.ServiceProvider.GetRequiredService<IEventStore>();
            (await Read(store.ReadEvents()).WaitAsync(Timeout)).Should().BeEmpty();
            var delivered = new List<Event>();
            var allDelivered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var expected = rollback ? new[] { 2, 3 } : new[] { 0, 1, 2, 3 };
            using var subscription = database.Services.GetRequiredService<IObservable<Event>>().Subscribe(e =>
            {
                delivered.Add(e);
                if (delivered.Count == expected.Length)
                    allDelivered.TrySetResult();
            }, error => allDelivered.TrySetException(error));
            database.Services.GetRequiredService<EventStream<Event>>().Start();

            gate.Release.TrySetResult();
            if (rollback)
            {
                Func<Task> failingFirst = () => first;
                await failingFirst.Should().ThrowAsync<InvalidOperationException>();
            }
            else
                await first.WaitAsync(Timeout);
            await second.WaitAsync(Timeout);
            await allDelivered.Task.WaitAsync(Timeout);
            delivered.Select(e => ((Entry)e.Payload).Ordinal).Should().Equal(expected);
            delivered.Select(e => e.Position).Should().Equal(Enumerable.Range(1, expected.Length).Select(i => (long)i));
            (await database.Counter()).Should().Be(expected.Length);
        }
        finally
        {
            gate.Release.TrySetResult();
            try { await first.WaitAsync(Timeout); } catch { /* Observe the injected rollback. */ }
            if (second != null)
                await second.WaitAsync(Timeout);
        }
    }

    [TestMethod]
    public async Task RejectsExternalTransactionsExplicitPositionsAndUnrelatedPendingChanges()
    {
        await using var database = await TestDatabase.Create();
        using var scope = database.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EventStoreContext>();
        var writer = scope.ServiceProvider.GetRequiredService<IEventWriter<DbEvent>>();
        await using (var transaction = await context.Database.BeginTransactionAsync())
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
        (await database.Counter()).Should().Be(0);
    }

    [TestMethod]
    public async Task NonUtcTimestampIsNormalizedAndEnumerationFailureAllocatesNothing()
    {
        await using var database = await TestDatabase.Create();
        using var scope = database.Services.CreateScope();
        var writer = scope.ServiceProvider.GetRequiredService<IEventWriter<DbEvent>>();
        var timestamp = new DateTimeOffset(2026, 10, 9, 12, 30, 0, TimeSpan.FromHours(5));
        await writer.WriteEvents([Raw("offset") with { Timestamp = timestamp }]);
        var persisted = await scope.ServiceProvider.GetRequiredService<EventStoreContext>().Events.SingleAsync();
        persisted.Timestamp.Should().Be(timestamp.ToUniversalTime());
        persisted.Timestamp.Offset.Should().Be(TimeSpan.Zero);

        IEnumerable<DbEvent> BrokenBatch()
        {
            yield return Raw("partial enumeration");
            throw new InvalidOperationException("Enumeration failed");
        }
        Func<Task> append = () => writer.WriteEvents(BrokenBatch());
        await append.Should().ThrowAsync<InvalidOperationException>().WithMessage("Enumeration failed");
        (await database.Counter()).Should().Be(1);
    }

    static DbEvent Raw(string payload) => new(0, "Raw", "A", "Raw.Entry", payload, DateTimeOffset.UtcNow);

    static async Task<List<Event>> Read(IAsyncEnumerable<Event> source)
    {
        var events = new List<Event>();
        await foreach (var e in source)
            events.Add(e);
        return events;
    }

    [SerializableEventPayload("PostgresTest.Entry")]
    public record Entry(int Ordinal, string JournalId, string Text)
        : EventPayload(new StreamId("PostgresTest", JournalId), "PostgresTest.Entry");

    sealed class TestDatabase(string connectionString, ServiceProvider services) : IAsyncDisposable
    {
        public ServiceProvider Services => services;

        public static async Task<TestDatabase> Create(IInterceptor? interceptor = null, int? maxBatchSize = null)
        {
            var configured = Environment.GetEnvironmentVariable("TEST_POSTGRES_CONNECTION_STRING");
            if (string.IsNullOrWhiteSpace(configured))
            {
                if (Environment.GetEnvironmentVariable("TEST_POSTGRES_REQUIRED") == "true")
                    Assert.Fail("PostgreSQL integration tests are required, but TEST_POSTGRES_CONNECTION_STRING is missing.");
                Assert.Inconclusive("Set TEST_POSTGRES_CONNECTION_STRING to run PostgreSQL integration tests.");
            }
            var connection = new NpgsqlConnectionStringBuilder(configured)
            {
                Database = $"eventsourcing_test_{Guid.NewGuid():N}",
                ApplicationName = $"eventsourcing_test_{Guid.NewGuid():N}"
            };
            var services = new ServiceCollection()
                .AddEventSourcing(options => options
                    .UsePostgresEventStore(_ => connection.ConnectionString)
                    .PayloadAssemblies(typeof(PostgresEventStoreTest).Assembly))
                .AddSingleton<EventPayloadMapper>(new CommandProcessedMapper<string, FailedSaveIsolationTest.ErrorPayload>())
                .AddDbContext<PostgresEventStoreContext>(options =>
                {
                    if (maxBatchSize.HasValue)
                        options.UseNpgsql(connection.ConnectionString, postgres => postgres.MaxBatchSize(maxBatchSize.Value));
                    if (interceptor != null)
                        options.AddInterceptors(interceptor);
                })
                .BuildServiceProvider();
            var database = new TestDatabase(connection.ConnectionString, services);
            try
            {
                using var scope = services.CreateScope();
                await scope.ServiceProvider.GetRequiredService<EventStoreContext>().Database.MigrateAsync();
                return database;
            }
            catch
            {
                await database.DisposeAsync();
                throw;
            }
        }

        public async Task<long> Counter()
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand("SELECT \"LastPosition\" FROM public.\"EventPositionCounter\" WHERE \"Id\" = 1", connection);
            return (long)(await command.ExecuteScalarAsync())!;
        }

        public async Task WaitForCounterLock()
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand("""
                SELECT EXISTS (
                    SELECT 1 FROM pg_stat_activity
                    WHERE datname = current_database() AND pid <> pg_backend_pid()
                      AND wait_event_type = 'Lock' AND query LIKE '%UPDATE public."EventPositionCounter"%'
                )
                """, connection);
            using var cancellation = new CancellationTokenSource(Timeout);
            while (!(bool)(await command.ExecuteScalarAsync(cancellation.Token))!)
                await Task.Delay(20, cancellation.Token);
        }

        public async ValueTask DisposeAsync()
        {
            await services.DisposeAsync();
            await using var context = new PostgresEventStoreContext(new DbContextOptionsBuilder<PostgresEventStoreContext>()
                .UseNpgsql(connectionString).Options);
            await context.Database.EnsureDeletedAsync();
        }
    }

    sealed class CountSaves : SaveChangesInterceptor
    {
        public int Attempts { get; private set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Attempts++;
            return ValueTask.FromResult(result);
        }
    }

    sealed class CaptureInserts : DbCommandInterceptor
    {
        public int ExecutedInserts { get; private set; }
        public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            DbDataReader result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("INSERT INTO public.\"Events\""))
                ExecutedInserts++;
            return ValueTask.FromResult(result);
        }

        public override ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            int result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("INSERT INTO public.\"Events\""))
                ExecutedInserts++;
            return ValueTask.FromResult(result);
        }
    }

    sealed class FailAfterSave : SaveChangesInterceptor
    {
        int attempts;
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = default)
        {
            if (++attempts == 1)
                throw new InvalidOperationException("Injected after-save failure");
            return ValueTask.FromResult(result);
        }
    }

    sealed class HoldFirstSave(bool rollback) : SaveChangesInterceptor
    {
        int attempts;
        public TaskCompletionSource Saved { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref attempts) == 1)
            {
                Saved.TrySetResult();
                await Release.Task.WaitAsync(Timeout, cancellationToken);
                if (rollback)
                    throw new InvalidOperationException("Injected competing-writer rollback");
            }
            return result;
        }
    }
}
