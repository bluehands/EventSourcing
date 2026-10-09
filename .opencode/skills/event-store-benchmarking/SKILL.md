---
name: event-store-benchmarking
description: Use when running event-store benchmarks, comparing provider performance or updating baseline reports in this project.
---

# Event-store benchmarking

1. Read `src/EventSourcing.Benchmarks/README.md` for CLI/metric definitions and the target baseline report for exact reproduction parameters. Build Release before using `--no-build`. Use `sqlite|sqlserver|postgres|all`; historical `both` is obsolete.
2. Match history, payload, batch sizes, worker counts, operations/worker and repetitions. Match machine/server, durability and preparation settings for before/after claims; document remaining differences. Run suites sequentially to avoid cross-suite contention. Smoke runs establish functionality, not performance.
3. Use only runner-owned isolated databases. Supply connections through `EVENTSOURCING_BENCHMARK_POSTGRES` / `EVENTSOURCING_BENCHMARK_SQLSERVER`; never publish credentials. Retain durability settings and record them. Verify database cleanup after runs, including failed evaluations; never reset or delete unrelated databases.
4. Check exit status, operation failures and persisted/read counts. Preserve failures and all repetitions; do not present faster failed runs as improvements. Counts do not establish input/commit ordering—use integration tests for those guarantees.
5. Report **appends/s separately from events/s**: one successful `WriteEvents` batch is one append. Command batches include `CommandProcessed`; direct benchmark writes do not automatically add it. Use group-active time for throughput and matching single-worker references for scaling. Mixed groups can finish at different times; short-run tails and warmup effects need context.
6. Preserve full aggregate tables, environment/code state, exact commands and artifact paths in the requested baseline Markdown: `artifacts/` is ignored by Git. Consult existing baseline results before repeating experiments. For large/small batch interference, report maximum small-append latency as well as percentiles; p99 can hide a single blocked append.

When running multiple evaluation candidates, clear retained Npgsql pools between candidates and rewarm before timing; otherwise their accumulated connections can exhaust the server limit. Process-wide allocation measurements include the harness and serialization, not just writer allocations or peak memory.
