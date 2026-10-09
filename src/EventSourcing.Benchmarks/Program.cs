using System.Diagnostics;
using System.Runtime.InteropServices;

namespace EventSourcing.Benchmarks;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Contains("--help"))
        {
            Console.WriteLine("""
                Event-store database benchmarks (run in Release)
                --provider sqlite|sqlserver|postgres|all  Default: sqlite
                --profile smoke|quick|full             Default: quick
                --scenarios global,tail,stream,write,mixed
                --workers 1,2,4,8 --batches 1,10,100,1000
                --payloads 256,1024,10240 --histories 10000,100000
                --operations N --repetitions N --streams N
                --readers N --writers N                Mixed workload overrides
                --output PATH                         Default: artifacts/benchmarks
                --keep-database                       Retain runner-owned databases
                SQL Server: set EVENTSOURCING_BENCHMARK_SQLSERVER to an instance connection string.
                PostgreSQL: set EVENTSOURCING_BENCHMARK_POSTGRES to an instance connection string.
                all selects SQLite, SQL Server and PostgreSQL.
                Each server-backed run creates and drops its own uniquely named database.
                """);
            return 0;
        }
        var measurements = new List<Measurement>();
        try
        {
            var options = Options.Parse(args);
            var directory = Path.GetFullPath(Path.Combine(options.Output, DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..8]));
            Directory.CreateDirectory(directory);
            var databases = new Dictionary<string, Dictionary<string, string>>();
            var metadata = new
            {
                StartedUtc = DateTime.UtcNow, Runtime = RuntimeInformation.FrameworkDescription,
                OS = RuntimeInformation.OSDescription, Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
                Machine = Environment.MachineName, Processors = Environment.ProcessorCount,
                GitRevision = await Git("rev-parse HEAD"), GitStatus = await Git("status --short"),
                Configuration = new { options.Provider, options.Profile, options.Scenarios, options.Workers, options.Batches,
                    options.Payloads, options.Histories, options.Operations, options.Repetitions, options.Streams, options.Readers, options.Writers },
                Databases = databases
            };
            Console.WriteLine($"Reports: {directory}");
            try
            {
                foreach (var provider in options.Providers())
                {
                    await using var database = new BenchmarkDatabase(provider, options, directory);
                    await database.Initialize();
                    databases.Add(provider, database.Metadata);
                    Console.WriteLine($"{provider}: {database.Name}");
                    foreach (var workload in options.Workloads().Distinct())
                    {
                        Console.WriteLine($"  {workload.Scenario}: history={workload.History}, bytes={workload.PayloadBytes}, batch={workload.BatchSize}, readers={workload.Readers}, writers={workload.Writers}");
                        await database.Reset(workload, options.Streams);
                        // Warm up the actual read/write paths and every worker; discard warmup samples.
                        await WorkloadRunner.Run(database, options with { Operations = 1 }, workload, 0);
                        for (var repetition = 1; repetition <= options.Repetitions; repetition++)
                        {
                            await database.Reset(workload, options.Streams);
                            var result = await WorkloadRunner.Run(database, options, workload, repetition);
                            measurements.Add(result);
                            Console.WriteLine($"    repetition {repetition}: {result.ElapsedSeconds:F3}s, failures={result.Samples.Count(s => s.Error != null)}");
                        }
                        await Report.Save(directory, metadata, measurements);
                    }
                }
            }
            finally
            {
                // Preserve completed measurements even if a later workload or cleanup fails.
                await Report.Save(directory, metadata, measurements);
            }
            Console.WriteLine($"Completed {measurements.Count} measurements. See summary.md, summary.csv and results.json.");
            return measurements.SelectMany(m => m.Samples).Any(s => s.Error != null) ? 2 : 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    static async Task<string?> Git(string arguments)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("git", arguments)
                { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false });
            if (process == null) return null;
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
            return process.ExitCode == 0 ? output.Trim() : null;
        }
        catch { return null; }
    }
}
