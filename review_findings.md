# Correctness review findings and remediation plan

## Scope and validation status

Original read-only static review of the core event-sourcing library, command processing, EF persistence, SQLite and SQL Server providers, source generation, and existing tests. No builds or tests were run during that review. Subsequent implementation and validation are recorded under each finding; regression tests without such a validation entry remain proposals.

Findings 1–9 describe defects or failure-handling gaps identified from the code. Finding 1 and follow-up 1a are resolved with validated discard-on-failure semantics. Point 10 is a separate ordering concern that needs verification before being treated as a confirmed defect. Follow-up 1b requires behavior decisions before implementation.

## 1. High — Failed saves contaminate subsequent writes

### Locations

- `src/EventSourcing.Persistence.EntityFramework/Infrastructure/EventStore.cs:26–29`
- `src/EventSourcing.Commands/Infrastructure/Internal/CommandRegistrationExtensions.cs:29–34,57–61`

### Incorrect behavior

`AddRangeAsync` attaches events to the scoped `DbContext`. If `SaveChangesAsync` fails, those events remain tracked as `Added`. The command error handler then uses the same event store and context to persist a faulted `CommandProcessed` event, inadvertently retrying the original batch too.

A transient failure can result in domain events, the original successful completion marker, and a fault marker being persisted together. A permanently invalid event can prevent the fault marker from being persisted. A later write in the same scope can also persist events from an earlier failed call.

### Proposed regression tests

1. Use SQLite and a one-shot `SaveChangesInterceptor` that throws before the first save, after serialization has succeeded.
2. Process a command producing a domain event.
3. Allow the fault-event fallback to succeed.
4. Read through a fresh context.
5. Assert that only the fault marker exists, with neither the domain event nor the successful completion marker.

Also verify that a subsequent independent write in the same scope does not implicitly retry the failed write's tracked entities.

### Proposed fix

The initial proposal was to isolate writes using fresh contexts or detach only entries belonging to the failed operation. The clarified contract is that `EventStoreContext` is dedicated to event persistence: a failed write abandons all pending changes in that context. Detach changed entries while retaining unchanged tracked entries, and allow the command fault marker to be persisted without retrying abandoned changes.

Do not infer that every batch passed to the event writer represents exactly one command execution.

### Resolution and validation — Completed 2026-10-08

The EF writer wraps both `AddRangeAsync` and `SaveChangesAsync` in one error boundary. On failure it snapshots and detaches all entries in `Added`, `Modified`, or `Deleted` state, then rethrows the original exception. Unchanged entries remain tracked. This replaces the initial targeted cleanup with the clarified dedicated-context contract: all pending event-persistence changes are abandoned on failure. The existing command fault-marker fallback can reuse the scoped context without implicitly retrying abandoned changes.

Added SQLite regressions in `src/EventSourcing.Test/FailedSaveIsolationTest.cs` for:

- A subsequent independent write in the same scope persisting only its own batch.
- Command fallback persisting only the faulted marker, without the domain event or successful marker.
- Failed-write cleanup discarding pre-existing added, modified, and deleted entries while retaining unchanged entries; a subsequent write leaves the original persisted rows intact.
- Failure serializing the second event persisting nothing immediately and leaving no attached partial batch for a subsequent write.

The same-scope and command-fallback regressions reproduced the original defect. The discard-all-pending-changes and second-event serialization regressions failed against the initial targeted cleanup and pass with the simplified fix. Ran `dotnet test "src/EventSourcing.Test/EventSourcing.Test.csproj"`: **7 passed, 0 failed** (four regression tests and three existing tests).

Partial mapping/serialization failures during attachment are covered by the same cleanup (follow-up 1a below); automatic retries remain follow-up 1b.

## 1a. Follow-up — Define behavior for partial serialization failures

This is separate from failed-save recovery.

If serialization fails on the second event in a collection, lazy mapping during `AddRangeAsync` may leave earlier entities attached. Before selecting a fix, decide the desired write contract: atomic writes, partial progress, or fully prepared inputs.

The event writer must not assume that each batch corresponds to one command execution.

### Proposed work

1. Define the intended behavior for mapping or serialization failures partway through a collection.
2. Add a test whose second event fails serialization.
3. Assert both the immediate persistence outcome and the effect on a subsequent write, according to the agreed contract.
4. Implement the chosen semantics. Fully materializing serialized events before attaching them is an option if preparation must be all-or-nothing.

### Resolution and validation — Completed 2026-10-08

Chosen contract: attachment, mapping, or serialization failures discard all pending changes in the dedicated EF event-store context and propagate the original exception. Serialization completes before saving begins, so a partial serialization failure persists no events from that batch. Cleanup also ensures a subsequent write cannot persist previously attached portions of the failed batch.

`SecondEventSerializationFailureDoesNotContaminateSubsequentWrite` uses a serializer that throws on its second call. It verifies the exception, an empty database through a fresh context, an empty tracker after failure, and persistence of only a later independent write. The test failed against the initial finding-1 fix and passes with the combined attachment/save error boundary.

## 1b. Follow-up — Define an event-write retry policy

This is separate from failed-save isolation in finding 1. The current EF writer calls `SaveChangesAsync` once, and neither provider configures `EnableRetryOnFailure`. The command layer makes one separate attempt to persist a fault marker after a write failure; it does not intentionally retry the original batch.

### Decisions required

- Define which provider-specific failures are transient and eligible for retry. Invalid data and serialization errors must not be retried as transient failures.
- Define retry limits, backoff, and how exhausted retries are surfaced to callers.
- Handle ambiguous commit outcomes: a connection failure during commit can leave the caller unsure whether the events were persisted. Blindly retrying can duplicate events; establish an idempotency strategy or a way to verify the original write's success.
- Choose the retry boundary in the persistence layer so direct event writes and command-generated writes receive consistent behavior. Account for any explicit transactions and provider execution strategies.
- Persist a command fault marker only after retries are exhausted and the original write's outcome has been resolved. Coordinate terminal persistence failures with finding 9.

### Proposed work

1. Complete failed-save isolation in finding 1 first. Every write attempt must be isolated from subsequent writes, including retries and fault-marker writes.
2. Define the retry and duplicate-prevention contract before enabling automatic retries.
3. Add deterministic tests for a transient failure followed by success, retry exhaustion, and a permanent failure that is not retried.
4. Cover a write that commits but reports failure to the caller; verify that recovery does not duplicate events or persist a contradictory command fault marker. Use provider integration coverage where required to validate actual commit behavior.
5. Verify the policy applies consistently to direct writes and command writes, and that independent writes still work after retry exhaustion.

## 2. High — EF readers do not guarantee event order

### Location

`src/EventSourcing.Persistence.EntityFramework/Infrastructure/EventStore.cs:8–23`

### Incorrect behavior

Both readers filter events but omit `OrderBy(e => e.Position)`. Relational databases do not guarantee result order without explicit ordering. This violates the deterministic-order requirement documented in `README.md:109–111`.

Polling advances from the last emitted event, assuming that it has the greatest position. Unordered results can produce incorrect projection state and duplicate delivery on subsequent polls.

### Proposed regression test

Use SQLite with `PRAGMA reverse_unordered_selects = ON` on the reader connection. Insert distinguishable events, read globally and by stream, and assert strictly ascending positions. Feed the reader into polling and assert that all events are delivered once and in order.

### Proposed fix

Add `.OrderBy(e => e.Position)` to both queries before `.AsAsyncEnumerable()`.

## 3. High — SQL Server batch writes bypass the exclusive-write interceptor

### Locations

- `src/EventSourcing.Persistence.EntityFramework.SqlServer/Infrastructure/Internal/ExclusiveWriteInterceptor.cs:29–35`
- `src/EventSourcing.Persistence.EntityFramework.SqlServer/Infrastructure/Internal/SqlServerEventStoreOptionsExtension.cs:26–30`

### Incorrect behavior

The regex adds `TABLOCKX` only to statements matching `INSERT INTO [Events]`. EF Core's SQL Server provider can generate `MERGE [Events] ...` for multi-row inserts with generated values, which applies to this identity-key table. Those writes bypass the intended serialization mechanism.

Without writer serialization, identity allocation and commit order can diverge. With `READ_COMMITTED_SNAPSHOT`, for example, a reader can observe a later committed position while an earlier-position transaction is still uncommitted. Advancing the cursor can then permanently miss the earlier event.

### Agreed clarification

`TABLOCKX` is a reasonable writer-serialization mechanism provided every relevant write acquires it before allocating positions and holds it through commit or rollback. Multi-statement writes must retain protection for the entire transaction. The identified defect is incomplete write-statement coverage, not that `TABLOCKX` is inherently unsuitable.

Snapshot readers seeing the previous committed state do not, by themselves, invalidate this mechanism when writers are correctly serialized.

### Proposed regression tests

- Capture generated SQL for single inserts, `MERGE` batches, and writes split across multiple statements. Verify that all relevant writes receive the intended protection.
- SQL Server integration test with `READ_COMMITTED_SNAPSHOT` enabled:
  1. Insert a batch in transaction A and hold it uncommitted.
  2. Attempt another batch in transaction B.
  3. Read before committing A.
  4. Assert B cannot become visible ahead of A.
  5. Commit and verify that every event is eventually delivered exactly once.

Use synchronization barriers to control transaction timing.

### Proposed fix

Retain the `TABLOCKX` mechanism if integration tests establish the required lock behavior. Extend coverage to the actual generated statements and verify transaction-wide protection. An explicit transaction-level lock is an alternative if statement rewriting cannot reliably provide that protection.

Ordering within a single batch is a separate verification topic in point 10.

## 4. High — Startup can return before replay finishes

### Locations

- `src/EventSourcing.Commands/Infrastructure/Internal/FunicularCommandsInitializer.cs:17–22,30–34`
- `src/EventSourcing/EventSourcingContext.cs:21–33`

### Incorrect behavior

Replay is awaited only when the initializer loop encounters an `AfterEventReplay` group. Without an initializer for that phase, the group does not exist and `WaitForReplayDone()` is never called. `CommandsInitializer.Initialize()` starts the no-op command and immediately returns.

`StartEventSourcing()` can therefore complete while projections are still replaying. The Meetup application has no after-replay initializer and can accept queries or commands against partially initialized state.

### Proposed regression test

Configure commands and a projection without an after-replay initializer. Prepopulate history and gate the reader with a `TaskCompletionSource`. Assert startup remains incomplete while replay is blocked, then completes only after historical state is applied and the no-op marker is observed.

### Proposed fix

Introduce an unconditional replay-completion barrier in the command-enabled lifecycle, after replay has started and the no-op has been scheduled. Do not make the barrier depend on application registration of an after-replay initializer.

## 5. High — Command-enabled startup must validate its event store

### Locations

- `src/EventSourcing.Commands/Infrastructure/Internal/FunicularCommandsInitializer.cs:30–34`
- `src/EventSourcing.Commands/Infrastructure/Internal/CommandRegistrationExtensions.cs:21–44`

### Incorrect behavior

Missing `IEventStore` registration is a configuration error that should fail startup. Currently, event-store resolution occurs during processing, outside the per-command `try/finally`. Resolution failure can terminate the Rx subscription and leak the command scope, while the bus continues accepting commands.

### Agreed clarification

Running commands without an event store is not meaningful, and a missing registration cannot be repaired at runtime. Validate early instead of treating it as a recoverable per-command error.

### Proposed regression test

Configure the command layer without an `IEventStore`, then call `StartEventSourcing()`. Assert immediate failure with a clear configuration exception and verify that no processor subscription was activated.

### Proposed fix

Resolve `IEventStore` in a startup scope before subscribing processors or accepting commands. Validate constructibility as well as registration; do not retain the scoped instance in a singleton.

Separately, move per-command service resolution inside the scope's `try/finally` so unexpected runtime construction failures still dispose the scope and are surfaced explicitly. Add an explicit terminal-error handler for the shared subscription.

## 6. Medium — Multiple persisted versions mapping to one domain payload break startup

### Location

`src/EventSourcing/Infrastructure/EventFactory.cs:26–31`

### Incorrect behavior

The factory cache uses domain payload types as keys but does not deduplicate types contributed by registered mappers. Two distinct persisted versions mapping to the same domain payload cause a duplicate-key exception during factory construction, even though their persisted event types are unique.

### Proposed regression test

Register two mappers with different serializable event-type attributes and the same domain payload type. Assert startup succeeds and both stored versions deserialize into the appropriate typed domain event.

### Proposed fix

Deduplicate domain payload types before building the factory dictionary:

```csharp
return payloadTypes
    .Distinct()
    .ToFrozenDictionary(p => p, BuildCreateEvent);
```

Continue enforcing persisted event-type uniqueness separately.

## 7. Medium — Generated code fails for global-namespace and nested types

### Location

`src/EventSourcing.Commands.SourceGenerator/Generator.cs:38,53–54,98–118,145`

### Incorrect behavior

The generator reconstructs type identities from namespace strings and simple names. Global-namespace extension classes produce invalid namespace declarations. Nested result types lose their containing types, and nested extension classes are emitted at namespace level instead of extending the actual class.

### Proposed regression tests

Use `CSharpGeneratorDriver` compilation fixtures for:

1. An attributed extension class in the global namespace.
2. A result type nested in a container.
3. An attributed extension class nested in a container.

Assert error-free resulting compilations and that generated methods belong to the intended types.

### Proposed fix

Preserve fully qualified type identities and containing declarations using Roslyn symbols. Omit namespace declarations for global-namespace types. If a declaration form is deliberately unsupported, emit a clear diagnostic instead of invalid source.

## 8. Medium — Skipped corrupt events do not advance stream checkpoints

### Locations

- `src/EventSourcing/EventStore.cs:64–69`
- `src/EventSourcing/Infrastructure/Internal/PollingObservable.cs:31–44`
- `src/EventSourcing.Persistence.EntityFramework.SqlServer/Infrastructure/BrokerNotificationEventStream.cs:81–84`

### Incorrect behavior

Cursor advancement depends on successfully mapped events. A corrupt tail is repeatedly read, deserialized, and reported because its positions never advance the checkpoint. An entirely corrupt history can be reread on every poll. The broker reader also calculates state from mapped results rather than consumed raw rows.

### Proposed regression test

Store valid position 1 and corrupt positions 2 and 3. Run several polls with a counting corruption handler. Assert each corrupt position is handled once and subsequent reads begin after position 3. Append valid position 4 and assert normal delivery. Cover an entirely corrupt history too.

### Proposed fix

Track the highest consumed raw database position independently of emitted domain events, using batch/checkpoint metadata or cursor tracking below mapping. If the corruption handler throws, do not advance past that event.

## 9. High — Command waiters never finish if both persistence attempts fail

### Locations

- `src/EventSourcing.Commands/Infrastructure/Internal/CommandRegistrationExtensions.cs:53–66`
- `src/EventSourcing.Commands/ICommandBus.cs:20–28`

### Incorrect behavior

If both the original write and the fault-marker write fail, the command is logged and discarded. Its waiter never receives a completion event or the terminal persistence failure. The wait API uses `CancellationToken.None` and leaves its subscription active.

An external `Task.WaitAsync(timeout)` stops the caller's wait but does not cancel the internal subscription.

### Proposed regression test

Use a store that fails both writes. Assert the wait API terminates with an explicit persistence failure, or can be cancelled through the API. Verify subscription cleanup, then recover the store and confirm a subsequent command completes normally.

### Proposed fix

Provide a terminal command-failure channel for failures that cannot be persisted, and observe it alongside processed events. Add cancellation-token overloads and dispose subscriptions on cancellation and send failure. Cancellation alone does not communicate the underlying persistence failure.

## 10. Follow-up — Verify event order within SQL Server write batches

### Locations

- `src/EventSourcing.Persistence.EntityFramework/Infrastructure/EventStore.cs:26–29`
- `src/EventSourcing.Commands/Infrastructure/Internal/CommandRegistrationExtensions.cs:23–24`

### Concern — not yet a reproduced defect

Passing events to `AddRangeAsync` in order does not by itself establish a contractual guarantee that SQL Server assigns identity positions in that order. EF may generate a multi-row `MERGE`; its synthetic input-position mapping associates returned identities with the correct entities but does not itself establish ascending identity allocation in input order.

Unordered `OUTPUT` rows alone do not prove reordered identity allocation. These are distinct guarantees and must be checked separately. No reordering has been demonstrated with the current provider.

`TABLOCKX` serializes competing writers but does not specify row execution order inside one statement.

### Potential impact

The command layer appends `CommandProcessed` after domain events. If its persisted position precedes a domain event, a waiter could complete before all effects reach the projection. Order-sensitive domain events could also replay incorrectly.

### Proposed verification

1. Capture SQL generated by the actual EF/SQL Server provider.
2. Write distinguishable ordered events followed by a completion marker.
3. Read with explicit `ORDER BY Position`.
4. Assert input order is preserved and the completion marker comes last.
5. Cover small batches and batches spanning multiple statements.
6. Check the documented ordering guarantees of the generated statements; repeated passing tests alone do not establish a contractual guarantee.

### Proposed fix if ordering is not guaranteed

Use a write strategy that explicitly preserves input order, such as sequential single-row inserts within one transaction while retaining writer serialization. Evaluate the performance cost before selecting an implementation.

Keep point 3 focused on incomplete write-statement coverage.

## Existing test weakness — Subscription occurs after writing

### Location

`src/EventSourcing.Test/HandleBadCasesTest.cs:116–125`

### Incorrect behavior

The helper writes before subscribing to the hot stream. The poller can publish in between, causing a timeout even when production behavior is correct.

### Proposed regression test and fix

Use a writer that waits until the poller has delivered an event before returning to expose the race deterministically. Create the observable subscription/task before writing, and cancel/dispose it if writing fails.

The test project currently contains three tests and references neither Commands nor SQL Server. Add appropriate project references or separate test projects for the proposed coverage.

## Implementation order

1. Failed-save isolation and cleanup (1).
2. Explicit read ordering (2).
3. SQL Server write-statement coverage and transaction-wide locking verification (3).
4. Unconditional startup replay barrier (4).
5. Startup event-store validation and per-command scope cleanup (5).
6. Terminal failure reporting and waiter cancellation/cleanup (9).
7. Mapper-version compatibility (6), generator cases (7), and raw-position checkpoints (8).
8. Independently verify within-batch ordering (10). Serialization-failure semantics (1a) were resolved alongside finding 1.
9. Design and validate an event-write retry policy (1b), including ambiguous commit recovery and duplicate prevention, after failed-save isolation is complete.

Use deterministic tests with interceptors, controlled readers, and synchronization barriers. SQL Server commit-order and batch-order behavior require actual SQL Server integration coverage; SQLite cannot validate those guarantees.
