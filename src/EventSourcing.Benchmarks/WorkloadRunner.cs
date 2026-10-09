using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace EventSourcing.Benchmarks;

public sealed record OperationSample(string Kind, int Worker, int Operation, double Milliseconds, long Events, string? Error);
public sealed record Measurement(string Provider, Workload Workload, int Repetition, double ElapsedSeconds,
    double ReadActiveSeconds, double WriteActiveSeconds, OperationSample[] Samples);

public static class WorkloadRunner
{
    public static async Task<Measurement> Run(BenchmarkDatabase database, Options options, Workload workload, int repetition)
    {
        var samples = new ConcurrentBag<OperationSample>();
        var startGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var ready = new CountdownEvent(workload.Readers + workload.Writers);
        var data = new string('x', workload.PayloadBytes);
        var readFinished = new ConcurrentBag<double>();
        var writeFinished = new ConcurrentBag<double>();
        var timer = new Stopwatch();

        Task Worker(int worker, bool write)
        {
            // Generate the complete worker input outside the measured interval.
            var payloads = write ? Enumerable.Range(0, options.Operations).Select(operation =>
                Enumerable.Range(0, workload.BatchSize).Select(i => (IEventPayload)new BenchmarkPayload(
                    $"writer-{worker}", operation * workload.BatchSize + i, data)).ToArray()).ToArray() : [];
            return Task.Run(async () =>
            {
                // Prime each worker's context and connection before the synchronized start.
                await using (var scope = database.Services.CreateAsyncScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<Persistence.EntityFramework.EventStoreContext>();
                    await context.Database.CanConnectAsync();
                    _ = scope.ServiceProvider.GetRequiredService<IEventStore>();
                }
                ready.Signal();
                await startGate.Task;
                for (var operation = 0; operation < options.Operations; operation++)
                {
                    var begin = Stopwatch.GetTimestamp();
                    long count = 0;
                    string? error = null;
                    try
                    {
                        await using var scope = database.Services.CreateAsyncScope();
                        var store = scope.ServiceProvider.GetRequiredService<IEventStore>();
                        if (write)
                        {
                            await store.WriteEvents(payloads[operation]);
                            count = workload.BatchSize;
                        }
                        else
                        {
                            var stream = $"seed-{worker % options.Streams}";
                            var events = workload.Scenario switch
                            {
                                "global" => store.ReadEvents(),
                                "tail" => store.ReadEvents(database.TailPosition),
                                _ => store.ReadEvents(new StreamId("Benchmark", stream))
                            };
                            await foreach (var _ in events) count++;
                            var expected = workload.Scenario switch
                            {
                                "global" => workload.History,
                                "tail" => database.TailCount,
                                _ => workload.History / options.Streams + (worker % options.Streams < workload.History % options.Streams ? 1 : 0)
                            };
                            if (count != expected) throw new InvalidOperationException($"Read count mismatch: expected {expected}, found {count}.");
                        }
                    }
                    catch (Exception exception)
                    {
                        error = Describe(exception);
                        count = 0;
                    }
                    samples.Add(new(write ? "write" : "read", worker, operation,
                        Stopwatch.GetElapsedTime(begin).TotalMilliseconds, count, error));
                }
                (write ? writeFinished : readFinished).Add(timer.Elapsed.TotalSeconds);
            });
        }

        var tasks = Enumerable.Range(0, workload.Readers).Select(i => Worker(i, false))
            .Concat(Enumerable.Range(0, workload.Writers).Select(i => Worker(i, true))).ToArray();
        // Await readiness without blocking a thread-pool thread (SQLite async I/O may execute synchronously).
        while (!ready.IsSet)
        {
            if (tasks.Any(t => t.IsFaulted))
            {
                startGate.TrySetResult();
                await Task.WhenAll(tasks);
            }
            await Task.Delay(1);
        }
        timer.Start();
        startGate.SetResult();
        await Task.WhenAll(tasks);
        timer.Stop();
        var ordered = samples.OrderBy(s => s.Kind).ThenBy(s => s.Worker).ThenBy(s => s.Operation).ToArray();
        await database.Verify(ordered.Where(s => s.Kind == "write").Sum(s => s.Events), workload.History);
        return new(database.Provider, workload, repetition, timer.Elapsed.TotalSeconds,
            readFinished.DefaultIfEmpty(0).Max(), writeFinished.DefaultIfEmpty(0).Max(), ordered);
    }

    static string Describe(Exception exception)
    {
        var root = exception.GetBaseException();
        return root switch
        {
            SqlException sql => $"SqlException:{sql.Number}",
            SqliteException sqlite => $"SqliteException:{sqlite.SqliteErrorCode}:{sqlite.SqliteExtendedErrorCode}",
            PostgresException postgres => $"PostgresException:{postgres.SqlState}",
            NpgsqlException npgsql => $"NpgsqlException: {npgsql.Message}",
            _ => $"{root.GetType().Name}: {root.Message}"
        };
    }
}
