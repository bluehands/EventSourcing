---
name: postgres-persistence
description: Use when changing PostgreSQL event persistence, position allocation, append transfers or polling integration tests in this project.
---

# PostgreSQL persistence

- Acquire the transaction-level advisory lock **before** sequence allocation; hold it through commit/rollback. Use the same lock protocol in both array and COPY paths and across application instances.
- The sequence requires `CACHE 1 NO CYCLE`. Cached connection-local values or an uncoordinated `nextval`/`setval` can violate polling-safe ordering. Rollback leaves gaps.
- Complete input serialization/materialization before locking. Keep arbitrary batch sizes supported; array/COPY thresholds are internal transfer policy, not public limits. COPY bounds transfer buffering, not total input memory.
- Verify both paths with input order/completion-marker checks, atomic failure (including commit failure), same-scope recovery, competing commit/rollback, and actual reactive polling across gaps. Observe server lock contention rather than relying on timing delays.
- Do not blindly retry ambiguous commit failures: the batch may already be committed.

Sources: `src/EventSourcing.Persistence.EntityFramework.Postgres/Infrastructure/PostgresEventWriter.cs`, `src/EventSourcing.Test/PostgresEventStoreTest.cs`, and the PostgreSQL section of the root `README.md`.
