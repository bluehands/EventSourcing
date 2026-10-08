# Event-store database benchmarks

This .NET 10 runner establishes before/after performance baselines through the production `IEventStore` and persistence providers. Measurements include mapping, JSON serialization/deserialization, EF, scope creation/disposal, and actual database I/O. The runner controls fixed work and repetitions instead of automatically calibrating an ever-growing write dataset.

## Run

From the repository root (PowerShell):

```powershell
dotnet run -c Release --project src/EventSourcing.Benchmarks -- --provider sqlite --profile quick

$env:EVENTSOURCING_BENCHMARK_SQLSERVER = 'Server=localhost;Integrated Security=True;TrustServerCertificate=True'
dotnet run -c Release --project src/EventSourcing.Benchmarks -- --provider both --profile smoke
```

SQL Server uses the supplied instance and creates a unique `EventSourcingBenchmark_<guid>` database with the existing migrations. The login needs permissions to create, use, truncate tables in, and drop that database. The supplied initial catalog is replaced; existing databases are never reset. SQLite uses a real file under the result directory, with WAL enabled by the existing migration. Runner-owned databases are removed after execution unless `--keep-database` is supplied. Results and metadata remain. Credentials/connection strings are not exported.

For installed LocalDB, an example connection is `Server=(localdb)\MSSQLLocalDB;Integrated Security=True;TrustServerCertificate=True`. LocalDB is a real SQL Server engine but does not represent a remote production server's networking or resources.

```powershell
# Focused write scaling baseline
dotnet run -c Release --project src/EventSourcing.Benchmarks -- --provider both --scenarios write --workers 1,2,4,8 --batches 1,10,100,1000 --operations 100 --repetitions 5

# Parallel reads and independently sized mixed groups
dotnet run -c Release --project src/EventSourcing.Benchmarks -- --provider sqlite --scenarios global,tail,stream --workers 1,2,4,8
dotnet run -c Release --project src/EventSourcing.Benchmarks -- --provider both --scenarios mixed --readers 4 --writers 2 --batches 100 --workers 1

dotnet run -c Release --project src/EventSourcing.Benchmarks -- --help
```

## Profiles and parameters

| Profile | History | Data bytes/event | Workers | Write batch sizes | Operations/worker | Repetitions |
|---|---|---|---|---|---|---|
| smoke | 100 | 1,024 | 1, 2 | 1, 10 | 2 | 1 |
| quick (default) | 10,000 | 1,024 | 1, 4 | 1, 100 | 20 | 3 |
| full | 10,000, 100,000 | 256, 1,024, 10,240 | 1, 2, 4, 8 | 1, 10, 100, 1,000 | 20 | 3 |

The full profile is a large Cartesian matrix and can take a long time. Filter it with `--scenarios`, `--workers`, `--batches`, `--histories`, and `--payloads`. All numeric options must be positive. `--streams` defaults to 10. Payload size is the ASCII `Data` field length, not total JSON or physical row size. Increase `--operations` for stable latency distributions; smoke results validate execution, not performance claims.

## Workloads

- **global**: each operation fully replays seeded history through the global reader.
- **tail**: each operation reads the last up to 1,000 seeded events from an actual stored position, inclusive.
- **stream**: each operation fully reads a seeded stream, distributed across workers.
- **write**: each operation commits one batch to a worker-specific stream.
- **mixed**: independent reader and writer groups run concurrently against the same table. Readers replay fixed seeded streams; writers append to separate streams. `--workers` sets both group sizes unless overridden with `--readers` / `--writers`.

Every operation creates a fresh DI scope/context. Workers prime contexts/connections before a synchronized start. All payload objects are prepared outside timing. Reads are fully enumerated and counts are checked. Successful writes are checked against the persisted table count after each run. The existing library's write strategy and SQL Server interceptor are used unchanged.

Schema migration, seeding, warmup, verification and reset are excluded from timing. Before each measured repetition the events table is cleared (including identity reset) and identical history is seeded through `IEventStore`. This is a warm-cache workload; file allocation, database statistics, OS caches and transaction logs are not reset. No background event stream or command processors are started.

SQLite async calls can execute synchronously; workers use separate thread-pool tasks to allow actual connection concurrency. SQLite still serializes writes and may wait up to the configured 30-second busy timeout. SQL Server retains the instance/model's default database configuration; isolation, RCSI and recovery settings are recorded, not overridden. Errors are not retried by the benchmark runner; provider-internal waiting remains part of measured latency.

The SQLite connection's 30-second command timeout controls provider lock retries. `PRAGMA busy_timeout` is recorded separately and can be zero because Microsoft.Data.Sqlite implements its own retries.

## Reports and comparison

Each run gets its own folder under `artifacts/benchmarks` (override with `--output`):

- `results.json`: metadata, every operation sample, per-repetition timings and aggregate summaries.
- `summary.csv`: one row per provider/workload/operation kind.
- `summary.md`: readable aggregate table.

Reported throughput is total successful events or operations divided by the sum of that group's active wall times across repetitions. For writes, operations are committed batches; for reads, operations are complete enumerations. Mixed readers and writers may finish at different times, so both active times and total workload time are retained. This is fixed-work concurrency, not a claim that contention persists for the full interval.

Latency samples include scope disposal. Median/p95/p99 use nearest-rank percentiles of successful operations. Failed operation samples and their latencies remain in JSON, with counts and classified errors in summaries. A small sample count makes tail percentiles noisy. Scaling compares event throughput against a matching single-worker workload (1 reader + 1 writer for mixed) when that baseline is included.

Metadata includes runtime, OS, processor count, machine, Git revision/status, workload parameters, database version and settings. Exit codes: **0** successful run, **1** setup/verification/runner failure, **2** completed with operation failures. Completed results are saved even if a later workload fails.

Save a Release baseline before fixing findings, then rerun identical arguments on the same machine/server and compare matching CSV rows. Keep server load, storage, power settings and database configuration comparable. Do not interpret faster runs with operation failures as improvements. This suite checks counts, not the review's event-order or commit-order correctness guarantees.

The measured baseline and reproduction parameters are in [baseline-2026-10-08.md](baseline-2026-10-08.md).

## Validation

```powershell
dotnet test src/EventSourcing.Benchmarks.Test -c Release
dotnet run -c Release --project src/EventSourcing.Benchmarks -- --provider both --profile smoke
```
