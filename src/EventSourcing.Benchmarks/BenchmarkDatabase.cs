using EventSourcing.Persistence.EntityFramework;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace EventSourcing.Benchmarks;

public sealed class BenchmarkDatabase : IAsyncDisposable
{
    readonly string connectionString;
    readonly string? sqlitePath;
    readonly bool keep;
    public string Provider { get; }
    public string Name { get; }
    public ServiceProvider Services { get; }
    public Dictionary<string, string> Metadata { get; } = [];
    public long TailPosition { get; private set; }
    public int TailCount { get; private set; }

    public BenchmarkDatabase(string provider, Options options, string directory)
    {
        Provider = provider;
        keep = options.KeepDatabase;
        Name = $"EventSourcingBenchmark_{Guid.NewGuid():N}";
        if (provider == "sqlite")
        {
            sqlitePath = Path.Combine(directory, Name + ".db");
            connectionString = new SqliteConnectionStringBuilder { DataSource = sqlitePath, Pooling = true, DefaultTimeout = 30 }.ToString();
        }
        else if (provider == "postgres")
        {
            connectionString = new NpgsqlConnectionStringBuilder(options.PostgresConnection) { Database = Name }.ToString();
        }
        else if (provider == "sqlserver")
        {
            var builder = new SqlConnectionStringBuilder(options.SqlConnection) { InitialCatalog = Name };
            connectionString = builder.ToString();
        }
        else
            throw new ArgumentException($"Unknown benchmark provider '{provider}'.", nameof(provider));
        Services = new ServiceCollection().AddLogging().AddEventSourcing(b =>
        {
            b.PayloadAssemblies(typeof(BenchmarkPayload).Assembly);
            if (provider == "sqlite") b.UseSqliteEventStore(connectionString);
            else if (provider == "postgres") b.UsePostgresEventStore(connectionString);
            else b.UseSqlServerEventStore(connectionString);
        }).BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    public async Task Initialize()
    {
        await using var scope = Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventStoreContext>();
        // Apply only the schema lifecycle; do not start polling or broker listeners.
        await context.Database.MigrateAsync();
        await context.Database.OpenConnectionAsync();
        var connection = context.Database.GetDbConnection();
        async Task<string> Scalar(string sql)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            return Convert.ToString(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture) ?? "";
        }
        Metadata["Database"] = Name;
        Metadata["Version"] = await Scalar(Provider switch
        {
            "sqlite" => "SELECT sqlite_version()",
            "postgres" => "SELECT version()",
            _ => "SELECT @@VERSION"
        });
        if (Provider == "sqlite")
        {
            Metadata["File"] = sqlitePath!;
            Metadata["CommandTimeoutSeconds"] = "30";
            foreach (var pragma in new[] { "journal_mode", "synchronous", "busy_timeout", "page_size" })
                Metadata[pragma] = await Scalar($"PRAGMA {pragma}");
        }
        else if (Provider == "postgres")
        {
            Metadata["CommandTimeoutSeconds"] = (context.Database.GetCommandTimeout() ?? 30).ToString(System.Globalization.CultureInfo.InvariantCulture);
            foreach (var setting in new[] { "transaction_isolation", "synchronous_commit", "fsync", "full_page_writes", "wal_level", "max_connections" })
                Metadata[setting] = await Scalar($"SHOW {setting}");
            Metadata["PositionAllocation"] = "Transactional counter; one EF save per batch";
        }
        else
        {
            Metadata["RCSI"] = await Scalar("SELECT is_read_committed_snapshot_on FROM sys.databases WHERE name = DB_NAME()");
            Metadata["SnapshotIsolation"] = await Scalar("SELECT snapshot_isolation_state_desc FROM sys.databases WHERE name = DB_NAME()");
            Metadata["RecoveryModel"] = await Scalar("SELECT recovery_model_desc FROM sys.databases WHERE name = DB_NAME()");
            Metadata["IsolationLevel"] = await Scalar("SELECT transaction_isolation_level FROM sys.dm_exec_sessions WHERE session_id = @@SPID");
        }
    }

    public async Task Reset(Workload workload, int streams)
    {
        await using (var scope = Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<EventStoreContext>();
            if (Provider == "postgres")
            {
                await using var transaction = await context.Database.BeginTransactionAsync();
                await context.Database.ExecuteSqlRawAsync("""
                    TRUNCATE TABLE public."Events";
                    UPDATE public."EventPositionCounter" SET "LastPosition" = 0 WHERE "Id" = 1;
                    """);
                await transaction.CommitAsync();
            }
            else
                await context.Database.ExecuteSqlRawAsync(Provider == "sqlite"
                    ? "DELETE FROM Events; DELETE FROM sqlite_sequence WHERE name = 'Events';"
                    : "TRUNCATE TABLE [Events];");
        }
        var data = new string('x', workload.PayloadBytes);
        for (var start = 0; start < workload.History; start += 1000)
        {
            IEventPayload[] payloads = Enumerable.Range(start, Math.Min(1000, workload.History - start))
                .Select(i => (IEventPayload)new BenchmarkPayload($"seed-{i % streams}", i, data)).ToArray();
            await using var scope = Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IEventStore>().WriteEvents(payloads);
        }
        await using var readScope = Services.CreateAsyncScope();
        var db = readScope.ServiceProvider.GetRequiredService<EventStoreContext>();
        var positions = await db.Events.OrderBy(e => e.Position).Select(e => e.Position).ToArrayAsync();
        if (positions.Length != workload.History) throw new InvalidOperationException("Seed count mismatch.");
        TailCount = Math.Min(1000, positions.Length);
        TailPosition = positions[^TailCount];
    }

    public async Task Verify(long successfulWrittenEvents, int history)
    {
        await using var scope = Services.CreateAsyncScope();
        var count = await scope.ServiceProvider.GetRequiredService<EventStoreContext>().Events.LongCountAsync();
        if (count != history + successfulWrittenEvents)
            throw new InvalidOperationException($"Persistence count mismatch: expected {history + successfulWrittenEvents}, found {count}.");
    }

    public async ValueTask DisposeAsync()
    {
        await Services.DisposeAsync();
        if (Provider == "sqlite")
        {
            SqliteConnection.ClearAllPools();
            if (!keep)
                foreach (var suffix in new[] { "", "-wal", "-shm" }) File.Delete(sqlitePath + suffix);
        }
        else if (Provider == "postgres")
        {
            if (keep) return;
            await using var context = new Persistence.EntityFramework.Postgres.Infrastructure.PostgresEventStoreContext(
                new DbContextOptionsBuilder<Persistence.EntityFramework.Postgres.Infrastructure.PostgresEventStoreContext>()
                    .UseNpgsql(connectionString).Options);
            await context.Database.EnsureDeletedAsync();
        }
        else
        {
            if (keep) return;
            var builder = new SqlConnectionStringBuilder(connectionString) { InitialCatalog = "master" };
            await using var connection = new SqlConnection(builder.ToString());
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            // Name is generated internally, never derived from user input.
            command.CommandText = $"IF DB_ID('{Name}') IS NOT NULL BEGIN ALTER DATABASE [{Name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{Name}]; END";
            await command.ExecuteNonQueryAsync();
        }
    }
}
