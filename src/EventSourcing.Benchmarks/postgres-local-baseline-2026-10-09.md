# Local PostgreSQL baseline — 2026-10-09

**42 measured repetitions, zero operation failures.** Read and persisted-write count checks passed. All three runner-owned databases were removed, verified against `pg_database` after execution.

## Environment and reproduction

PostgreSQL 18.6, native Windows x64 server on localhost port 5432. .NET 10.0.12, Windows x64, 16 logical processors. Read committed, `synchronous_commit=on`, `fsync=on`, `full_page_writes=on`, `wal_level=replica`, `max_connections=100`. Current PostgreSQL provider: transactional batch-position counter and one EF save per append.

Same workload parameters as the [Docker PostgreSQL comparison](postgres-comparison-2026-10-09.md): 10,000 seeded events, 10 streams, 1,024 ASCII data bytes/event, workers 1 and 4, three repetitions. Reads use 20 operations/worker; writes and mixed use 100. The historical [SQLite/SQL Server baseline](baseline-2026-10-08.md) used 1,000 operations/worker for writes, so duration and table growth differ for that comparison.

```powershell
$env:EVENTSOURCING_BENCHMARK_POSTGRES = 'Host=localhost;Port=5432;Username=postgres;Password=your-password'
dotnet run -c Release --no-build --project src/EventSourcing.Benchmarks -- --provider postgres --scenarios global,tail,stream --workers 1,4 --operations 20 --repetitions 3
dotnet run -c Release --no-build --project src/EventSourcing.Benchmarks -- --provider postgres --scenarios write --workers 1,4 --batches 1,10,100 --operations 100 --repetitions 3
dotnet run -c Release --no-build --project src/EventSourcing.Benchmarks -- --provider postgres --scenarios mixed --workers 1,4 --batches 100 --operations 100 --repetitions 3
```

Artifacts under `artifacts/benchmarks/`:

| Suite | Directory | Repetitions |
|---|---|---:|
| Reads | `20261009-065317-4536de0d` | 18 |
| Writes | `20261009-065355-01111d43` | 18 |
| Mixed | `20261009-065428-5446ea90` | 6 |

## Throughput comparison

Aggregate events/second, rounded. SQLite and SQL Server are the saved October 8 baseline; Docker PostgreSQL is the earlier October 9 run. No provider code changes were made between the two PostgreSQL runs.

| Workload | R/W | Batch | SQLite | SQL Server | Docker PostgreSQL | Local PostgreSQL |
|---|---|---:|---:|---:|---:|---:|
| Global read | 1/0 | — | 181,203 | 89,763 | 103,499 | 169,178 |
| Tail read | 1/0 | — | 266,275 | 81,061 | 96,996 | 247,676 |
| Stream read | 1/0 | — | 148,807 | 71,214 | 105,594 | 180,546 |
| Global read | 4/0 | — | 688,085 | 337,160 | 182,419 | 784,297 |
| Tail read | 4/0 | — | 803,910 | 319,060 | 187,205 | 613,782 |
| Stream read | 4/0 | — | 377,771 | 256,617 | 179,784 | 506,406 |
| Write | 0/1 | 1 | 1,096 | 1,685 | 162 | 587 |
| Write | 0/1 | 10 | 9,156 | 10,280 | 1,556 | 5,619 |
| Write | 0/1 | 100 | 25,721 | 17,504 | 9,758 | 20,692 |
| Write | 0/4 | 1 | 1,326 | 4,993 | 224 | 1,505 |
| Write | 0/4 | 10 | 8,715 | 20,791 | 2,012 | 9,101 |
| Write | 0/4 | 100 | 25,018 | 22,251 | 11,317 | 22,632 |
| Mixed read | 1/1 | 100 | 83,260 | 91,572 | 84,935 | 98,838 |
| Mixed write | 1/1 | 100 | 13,557 | 17,541 | 7,734 | 10,571 |
| Mixed read | 4/4 | 100 | 248,719 | 171,200 | 163,907 | 436,290 |
| Mixed write | 4/4 | 100 | 17,139 | 18,503 | 8,641 | 19,067 |

## Local PostgreSQL latency

Milliseconds per read enumeration or committed append batch.

| Workload | R/W | Batch | Median | p95 | p99 |
|---|---|---:|---:|---:|---:|
| Global read | 1/0 | — | 45.190 | 107.020 | 120.285 |
| Tail read | 1/0 | — | 3.538 | 5.070 | 18.443 |
| Stream read | 1/0 | — | 4.805 | 7.007 | 22.297 |
| Global read | 4/0 | — | 48.845 | 59.887 | 75.241 |
| Tail read | 4/0 | — | 5.097 | 10.441 | 15.383 |
| Stream read | 4/0 | — | 6.305 | 14.045 | 23.449 |
| Write | 0/1 | 1 | 1.620 | 2.461 | 3.254 |
| Write | 0/1 | 10 | 1.604 | 2.978 | 3.915 |
| Write | 0/1 | 100 | 4.663 | 6.332 | 7.506 |
| Write | 0/4 | 1 | 2.353 | 4.483 | 6.450 |
| Write | 0/4 | 10 | 3.946 | 7.712 | 10.186 |
| Write | 0/4 | 100 | 15.313 | 33.969 | 48.373 |
| Mixed read | 1/1 | 100 | 9.058 | 17.987 | 23.441 |
| Mixed write | 1/1 | 100 | 8.456 | 15.550 | 22.215 |
| Mixed read | 4/4 | 100 | 7.946 | 20.011 | 27.825 |
| Mixed write | 4/4 | 100 | 17.998 | 38.905 | 51.532 |

## Observations

- Native-local throughput was higher than Docker PostgreSQL in every measured case. Batch-100 write throughput improved about 2x; four-reader global replay improved 4.3x. The previous run's environment materially affected its results.
- Single-writer batch-100 throughput was between SQLite and SQL Server. At four writers, PostgreSQL was approximately 1.7% above SQL Server and 9.5% below SQLite's historical baseline.
- Small appends still carry fixed overhead: single-event median fell from Docker's 6.145 ms to 1.620 ms, but remained above SQLite's 0.795 ms and SQL Server's 0.530 ms historical medians.
- Four-reader global and stream throughput exceeded both historical baselines; tail throughput exceeded SQL Server but remained below SQLite.
- Four-reader/four-writer mixed throughput exceeded both historical baselines for both reads and writes.
- The first single-reader global repetition took 2.026 seconds, versus 0.711 and 0.809 seconds for later repetitions. All samples are retained; its 4.636x four-reader scaling factor is inflated by that slower single-reader reference. Short runs remain sensitive to warmup/JIT and server cache effects.

This is an environment-aligned rerun of the quick PostgreSQL comparison, not a fresh equal-duration three-engine baseline. SQLite/SQL Server figures were recorded before the correctness-review fixes; server configuration, code revision, run duration, and system load still differ. These comparisons do not isolate individual engine or allocator costs.
