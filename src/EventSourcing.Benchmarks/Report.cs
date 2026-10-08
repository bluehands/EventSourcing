using System.Globalization;
using System.Text;
using System.Text.Json;

namespace EventSourcing.Benchmarks;

public sealed record Metrics(long Events, int SuccessfulOperations, int FailedOperations, double EventsPerSecond,
    double OperationsPerSecond, double MedianMilliseconds, double P95Milliseconds, double P99Milliseconds)
{
    public static Metrics Calculate(IEnumerable<OperationSample> samples, double seconds)
    {
        var all = samples.ToArray();
        var successful = all.Where(s => s.Error == null).ToArray();
        var latencies = successful.Select(s => s.Milliseconds).Order().ToArray();
        double Percentile(double percentile) => latencies.Length == 0 ? 0 : latencies[Math.Max(0, (int)Math.Ceiling(percentile * latencies.Length) - 1)];
        var events = successful.Sum(s => s.Events);
        return new(events, successful.Length, all.Length - successful.Length,
            seconds > 0 ? events / seconds : 0, seconds > 0 ? successful.Length / seconds : 0,
            Percentile(.5), Percentile(.95), Percentile(.99));
    }
}

public sealed record Summary(string Provider, Workload Workload, string Kind, int Repetitions,
    double ActiveSeconds, double TotalSeconds, Metrics Metrics, Dictionary<string, int> Errors, double? Scaling);

public static class Report
{
    public static Summary[] Summarize(IEnumerable<Measurement> measurements)
    {
        var summaries = measurements.GroupBy(m => (m.Provider, m.Workload)).SelectMany(group =>
            new[] { "read", "write" }.Where(kind => kind == "read" ? group.Key.Workload.Readers > 0 : group.Key.Workload.Writers > 0)
                .Select(kind =>
                {
                    var samples = group.SelectMany(m => m.Samples).Where(s => s.Kind == kind).ToArray();
                    var seconds = group.Sum(m => kind == "read" ? m.ReadActiveSeconds : m.WriteActiveSeconds);
                    return new Summary(group.Key.Provider, group.Key.Workload, kind, group.Count(), seconds,
                        group.Sum(m => m.ElapsedSeconds), Metrics.Calculate(samples, seconds),
                        samples.Where(s => s.Error != null).GroupBy(s => s.Error!).ToDictionary(g => g.Key, g => g.Count()), null);
                })).ToArray();
        return summaries.Select(summary =>
        {
            var baselineWorkload = summary.Workload with
            {
                Readers = summary.Workload.Readers > 0 ? 1 : 0,
                Writers = summary.Workload.Writers > 0 ? 1 : 0
            };
            var baseline = summaries.FirstOrDefault(s => s.Provider == summary.Provider && s.Workload == baselineWorkload && s.Kind == summary.Kind);
            return summary with { Scaling = baseline?.Metrics.EventsPerSecond > 0 ? summary.Metrics.EventsPerSecond / baseline.Metrics.EventsPerSecond : null };
        }).ToArray();
    }

    public static async Task Save(string directory, object metadata, IReadOnlyCollection<Measurement> measurements)
    {
        var summaries = Summarize(measurements);
        var json = new JsonSerializerOptions { WriteIndented = true };
        await File.WriteAllTextAsync(Path.Combine(directory, "results.json"), JsonSerializer.Serialize(new { metadata, summaries, measurements }, json));
        static string Number(double n) => n.ToString("F3", CultureInfo.InvariantCulture);
        static string Escape(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
        var csv = new StringBuilder("provider,scenario,history,payload_bytes,batch_size,readers,writers,kind,repetitions,active_seconds,total_seconds,events,successful_operations,failed_operations,events_per_second,operations_per_second,median_ms,p95_ms,p99_ms,scaling,errors\n");
        var text = new StringBuilder("# Event-store benchmark results\n\nThroughput uses each worker group's active wall time. Latencies include scope creation, mapping, database I/O and scope disposal; percentiles cover successful operations. Mixed groups may finish at different times. Scaling compares with a matching 1-worker (1 reader + 1 writer for mixed) workload when present.\n\n");
        text.AppendLine("| Provider | Scenario | History | Bytes | Batch | R/W | Kind | Events/s | Ops/s | Median ms | p95 ms | p99 ms | Failures | Scaling |");
        text.AppendLine("|---|---|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|---:|");
        foreach (var s in summaries)
        {
            var w = s.Workload;
            var m = s.Metrics;
            csv.AppendLine(string.Join(',', s.Provider, w.Scenario, w.History, w.PayloadBytes, w.BatchSize, w.Readers, w.Writers,
                s.Kind, s.Repetitions, Number(s.ActiveSeconds), Number(s.TotalSeconds), m.Events, m.SuccessfulOperations,
                m.FailedOperations, Number(m.EventsPerSecond), Number(m.OperationsPerSecond), Number(m.MedianMilliseconds),
                Number(m.P95Milliseconds), Number(m.P99Milliseconds), s.Scaling is { } scale ? Number(scale) : "", Escape(JsonSerializer.Serialize(s.Errors))));
            text.AppendLine($"| {s.Provider} | {w.Scenario} | {w.History} | {w.PayloadBytes} | {w.BatchSize} | {w.Readers}/{w.Writers} | {s.Kind} | {Number(m.EventsPerSecond)} | {Number(m.OperationsPerSecond)} | {Number(m.MedianMilliseconds)} | {Number(m.P95Milliseconds)} | {Number(m.P99Milliseconds)} | {m.FailedOperations} | {(s.Scaling is { } factor ? Number(factor) : "—")} |");
        }
        await File.WriteAllTextAsync(Path.Combine(directory, "summary.csv"), csv.ToString());
        await File.WriteAllTextAsync(Path.Combine(directory, "summary.md"), text.ToString());
    }
}
