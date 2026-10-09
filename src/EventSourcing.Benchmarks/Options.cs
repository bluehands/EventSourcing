namespace EventSourcing.Benchmarks;

public sealed record Options
{
    public string Provider { get; init; } = "sqlite";
    public string Profile { get; init; } = "quick";
    public string Output { get; init; } = "artifacts/benchmarks";
    public string? SqlConnection { get; init; } = Environment.GetEnvironmentVariable("EVENTSOURCING_BENCHMARK_SQLSERVER");
    public string? PostgresConnection { get; init; } = Environment.GetEnvironmentVariable("EVENTSOURCING_BENCHMARK_POSTGRES");
    public string[] Scenarios { get; init; } = ["global", "tail", "stream", "write", "mixed"];
    public int[] Workers { get; init; } = [1, 4];
    public int[] Batches { get; init; } = [1, 100];
    public int[] Payloads { get; init; } = [1024];
    public int[] Histories { get; init; } = [10000];
    public int Operations { get; init; } = 20;
    public int Repetitions { get; init; } = 3;
    public int Streams { get; init; } = 10;
    public int? Readers { get; init; }
    public int? Writers { get; init; }
    public bool KeepDatabase { get; init; }

    public static Options Parse(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--keep-database") { values.Add(args[i], "true"); continue; }
            if (!args[i].StartsWith("--") || i + 1 == args.Length || args[i + 1].StartsWith("--"))
                throw new ArgumentException($"Expected --option value, got '{args[i]}'. Use --help.");
            values.Add(args[i], args[++i]);
        }
        string[] allowed = ["--provider", "--profile", "--output", "--scenarios", "--workers", "--batches", "--payloads", "--histories", "--operations", "--repetitions", "--streams", "--readers", "--writers", "--keep-database"];
        foreach (var key in values.Keys)
            if (!allowed.Contains(key)) throw new ArgumentException($"Unknown option {key}.");
        var profile = values.GetValueOrDefault("--profile", "quick");
        var options = profile switch
        {
            "quick" => new Options(),
            "smoke" => new Options { Profile = profile, Workers = [1, 2], Batches = [1, 10], Histories = [100], Operations = 2, Repetitions = 1 },
            "full" => new Options { Profile = profile, Workers = [1, 2, 4, 8], Batches = [1, 10, 100, 1000], Payloads = [256, 1024, 10240], Histories = [10000, 100000] },
            _ => throw new ArgumentException("Profile must be smoke, quick, or full.")
        };
        int Positive(string key, int fallback) => values.TryGetValue(key, out var text) ? ParsePositive(text) : fallback;
        int[] List(string key, int[] fallback) => values.TryGetValue(key, out var text) ? text.Split(',').Select(ParsePositive).Distinct().Order().ToArray() : fallback;
        options = options with
        {
            Provider = values.GetValueOrDefault("--provider", options.Provider),
            Output = values.GetValueOrDefault("--output", options.Output),
            Scenarios = values.TryGetValue("--scenarios", out var scenarios) ? scenarios.Split(',').Distinct().ToArray() : options.Scenarios,
            Workers = List("--workers", options.Workers), Batches = List("--batches", options.Batches),
            Payloads = List("--payloads", options.Payloads), Histories = List("--histories", options.Histories),
            Operations = Positive("--operations", options.Operations), Repetitions = Positive("--repetitions", options.Repetitions),
            Streams = Positive("--streams", options.Streams),
            Readers = values.ContainsKey("--readers") ? Positive("--readers", 1) : null,
            Writers = values.ContainsKey("--writers") ? Positive("--writers", 1) : null,
            KeepDatabase = values.ContainsKey("--keep-database")
        };
        if (options.Provider is not ("sqlite" or "sqlserver" or "postgres" or "all")) throw new ArgumentException("Provider must be sqlite, sqlserver, postgres, or all.");
        if (options.Providers().Contains("sqlserver") && string.IsNullOrWhiteSpace(options.SqlConnection))
            throw new ArgumentException("Set EVENTSOURCING_BENCHMARK_SQLSERVER to an instance connection string.");
        if (options.Providers().Contains("postgres") && string.IsNullOrWhiteSpace(options.PostgresConnection))
            throw new ArgumentException("Set EVENTSOURCING_BENCHMARK_POSTGRES to an instance connection string.");
        if (options.Scenarios.Any(s => s is not ("global" or "tail" or "stream" or "write" or "mixed")))
            throw new ArgumentException("Scenarios: global,tail,stream,write,mixed.");
        return options;
    }

    static int ParsePositive(string text) => int.TryParse(text, out var value) && value > 0
        ? value : throw new ArgumentException($"Expected a positive integer, got '{text}'.");

    public string[] Providers() => Provider == "all" ? ["sqlite", "sqlserver", "postgres"] : [Provider];

    public IEnumerable<Workload> Workloads()
    {
        foreach (var history in Histories)
        foreach (var payload in Payloads)
        foreach (var workers in Workers)
        foreach (var scenario in Scenarios)
        foreach (var batch in scenario is "write" or "mixed" ? Batches : [0])
            yield return new(scenario, history, payload, batch,
                scenario == "write" ? 0 : scenario == "mixed" ? Readers ?? workers : workers,
                scenario == "write" ? workers : scenario == "mixed" ? Writers ?? workers : 0);
    }
}

public sealed record Workload(string Scenario, int History, int PayloadBytes, int BatchSize, int Readers, int Writers);
