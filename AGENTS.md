# Essential persistence invariants

- **Ordered, atomic batches:** persist events in input order, with `CommandProcessed` last for command appends. Commit the whole batch atomically; acknowledge success only after durable commit.
- **Polling-safe publication:** once a higher position is visible, no lower position may become visible later. Pollers advance their checkpoint past observed positions; late lower positions are permanently missed. Sequence/identity allocation order alone does not ensure commit order. Gaps are acceptable; reads must use position comparisons, not assume contiguous positions.

For PostgreSQL persistence changes, load `postgres-persistence`. For benchmark execution, comparison or reporting, load `event-store-benchmarking`. Project skills live in `.opencode/skills/`.

# Documentation

- The root `README.md` is the GitHub-facing entry point and advertisement. Keep it focused on the project's value, capabilities, and approachable getting-started guidance. Do not add implementation details, database administration instructions, benchmark tables, or other deep technical documentation there.
- Put technical details in the relevant project folder or dedicated documentation, and use concise links from the root README when useful. Benchmark plans and result overviews belong in `src/EventSourcing.Benchmarks/`.
