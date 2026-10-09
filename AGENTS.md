# Essential persistence invariants

- **Ordered, atomic batches:** persist events in input order, with `CommandProcessed` last for command appends. Commit the whole batch atomically; acknowledge success only after durable commit.
- **Polling-safe publication:** once a higher position is visible, no lower position may become visible later. Pollers advance their checkpoint past observed positions; late lower positions are permanently missed. Sequence/identity allocation order alone does not ensure commit order. Gaps are acceptable; reads must use position comparisons, not assume contiguous positions.

For PostgreSQL persistence changes, load `postgres-persistence`. For benchmark execution, comparison or reporting, load `event-store-benchmarking`. Project skills live in `.opencode/skills/`.
