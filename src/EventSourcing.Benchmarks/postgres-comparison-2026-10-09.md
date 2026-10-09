# PostgreSQL quick comparison — 2026-10-09

**42 measured repetitions, zero operation failures.** Read and persisted-write count checks passed. Compared with the saved [SQLite/SQL Server baseline](baseline-2026-10-08.md).

## Environment and parameters

PostgreSQL 18.6 in Docker Desktop (Linux container), reached from the Windows runner over localhost TCP. .NET 10.0.12, Windows x64, 16 logical processors. PostgreSQL defaults: read committed, `synchronous_commit=on`, `fsync=on`, `full_page_writes=on`, `wal_level=replica`, `max_connections=100`. Production transactional counter allocation and one EF save per append batch.

All workloads: 10,000 seeded events, 10 streams, 1,024 ASCII data bytes/event, workers 1 and 4, three repetitions. Reads use 20 operations/worker; mixed uses 100, matching the baseline. Writes use **100 operations/worker rather than the baseline's 1,000**, to keep this run short. This changes duration and table growth. Eight-worker cases were not run.

```powershell
$env:EVENTSOURCING_BENCHMARK_POSTGRES = 'Host=localhost;Port=55432;Username=postgres;Password=your-password'
dotnet run -c Release --no-build --project src/EventSourcing.Benchmarks -- --provider postgres --scenarios global,tail,stream --workers 1,4 --operations 20 --repetitions 3
dotnet run -c Release --no-build --project src/EventSourcing.Benchmarks -- --provider postgres --scenarios write --workers 1,4 --batches 1,10,100 --operations 100 --repetitions 3
dotnet run -c Release --no-build --project src/EventSourcing.Benchmarks -- --provider postgres --scenarios mixed --workers 1,4 --batches 100 --operations 100 --repetitions 3
```

Artifacts under `artifacts/benchmarks/`:

| Suite | Directory | Repetitions |
|---|---|---:|
| Reads | `20261009-064226-58e86e83` | 18 |
| Writes | `20261009-064323-2d1fad55` | 18 |
| Mixed | `20261009-064428-7413c92b` | 6 |

Each directory contains `summary.md`, `summary.csv`, and `results.json`. Runner-owned databases and the temporary container were removed.

## Throughput comparison

All values are aggregate **events/second**, rounded to whole events. SQLite and SQL Server columns are the historical baseline, not fresh runs.

### Reads

| Scenario | Readers | SQLite baseline | SQL Server baseline | PostgreSQL |
|---|---:|---:|---:|---:|
| Global | 1 | 181,203 | 89,763 | 103,499 |
| Tail | 1 | 266,275 | 81,061 | 96,996 |
| Stream | 1 | 148,807 | 71,214 | 105,594 |
| Global | 4 | 688,085 | 337,160 | 182,419 |
| Tail | 4 | 803,910 | 319,060 | 187,205 |
| Stream | 4 | 377,771 | 256,617 | 179,784 |

### Writes

| Batch | Writers | SQLite baseline | SQL Server baseline | PostgreSQL |
|---|---:|---:|---:|---:|
| 1 | 1 | 1,096 | 1,685 | 162 |
| 10 | 1 | 9,156 | 10,280 | 1,556 |
| 100 | 1 | 25,721 | 17,504 | 9,758 |
| 1 | 4 | 1,326 | 4,993 | 224 |
| 10 | 4 | 8,715 | 20,791 | 2,012 |
| 100 | 4 | 25,018 | 22,251 | 11,317 |

### Mixed, batch 100

| Readers/writers | Kind | SQLite baseline | SQL Server baseline | PostgreSQL |
|---|---|---:|---:|---:|
| 1/1 | Read | 83,260 | 91,572 | 84,935 |
| 1/1 | Write | 13,557 | 17,541 | 7,734 |
| 4/4 | Read | 248,719 | 171,200 | 163,907 |
| 4/4 | Write | 17,139 | 18,503 | 8,641 |

## Selected write latency

Milliseconds per committed batch; percentiles cover successful operations.

| Workload | R/W | SQLite median / p95 | SQL Server median / p95 | PostgreSQL median / p95 |
|---|---|---|---|---|
| Write batch 1 | 0/1 | 0.795 / 1.567 | 0.530 / 0.967 | 6.145 / 7.295 |
| Write batch 100 | 0/1 | 3.356 / 5.911 | 5.459 / 7.426 | 9.767 / 11.902 |
| Write batch 100 | 0/4 | 3.410 / 11.707 | 17.665 / 23.301 | 27.278 / 66.592 |
| Mixed batch 100 | 1/1 | 5.906 / 13.378 | 5.395 / 7.607 | 12.761 / 16.568 |
| Mixed batch 100 | 4/4 | 5.230 / 9.606 | 19.969 / 35.978 | 33.815 / 111.290 |

## Interpretation

- Single-reader PostgreSQL throughput was above SQL Server's baseline and below SQLite's for all three read patterns.
- Four-reader PostgreSQL throughput was below both baselines. Scaling was 1.70–1.93x, versus 2.54–3.80x for SQLite and 3.60–3.94x for SQL Server.
- Batching improved PostgreSQL single-writer throughput from 162 to 9,758 events/s. Median batch latency increased from 6.145 to 9.767 ms: fixed append overhead dominates small batches in this environment.
- Batch-100 PostgreSQL write throughput was approximately 38% of SQLite and 56% of SQL Server at one writer; 45% and 51% respectively at four writers. Four writers improved PostgreSQL batch-100 throughput only 1.16x, consistent with serialized append transactions.
- Mixed read throughput was close to both baselines at 1/1 and close to SQL Server at 4/4. Mixed write throughput was roughly half the baselines.

These results compare the current PostgreSQL provider with yesterday's pre-correctness-fix baseline, which used file-backed SQLite and SQL Server Express LocalDB, not Docker/TCP. Differences therefore combine provider, networking, storage, server configuration, code revision, and run duration. The counter, network round trips, and durable commits are potential contributors to fixed write cost, but this run does not isolate their individual costs. A fresh, equal-duration, same-environment run is needed before attributing the difference to the database engine or optimizing a specific component.
