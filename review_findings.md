# Correctness review findings and remediation plan

## Scope and validation status

Original read-only static review of the core event-sourcing library, command processing, EF persistence, SQLite and SQL Server providers, source generation, and existing tests. No builds or tests were run during that review. Subsequent implementation and validation are recorded under each finding; regression tests without such a validation entry remain proposals.

Findings 1–9 record the original defects or failure-handling gaps and their subsequent reassessments. Findings 1/1a, 2, 3, 6, 7 (under its narrowed scope), 9, and 10 are resolved. Finding 4's startup/readiness documentation and Meetup readiness gate are implemented. Finding 5's startup validation is addressed by existing normal polling behavior; runtime cleanup and subscription-error handling remain open. Finding 8 remains open. Follow-up 1b requires behavior decisions before implementation.

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

### Reproduction and validation — 2026-10-08

Added `src/EventSourcing.Test/EventReadOrderingTest.cs` using SQLite's connection-local `PRAGMA reverse_unordered_selects = ON` on an explicitly opened reader connection. Five events are written individually across two interleaved streams, establishing known positions without relying on within-batch insertion order.

- Four reader cases cover global and stream reads, starting at positions 0 and 3. All fail because results arrive in descending position order.
- A polling regression observes two complete reads through the real event store. Instead of delivering positions `1, 2, 3, 4, 5` exactly once, it delivers `5, 4, 3, 2, 1, 5, 4, 3, 2`: the second poll starts at position 2 because position 1 was emitted last.

Ran `dotnet test "src/EventSourcing.Test/EventSourcing.Test.csproj" --filter "FullyQualifiedName~EventReadOrderingTest"`: **5 failed**, reproducing the defect. Ran the remaining tests with `--filter "FullyQualifiedName!~EventReadOrderingTest"`: **7 passed, 0 failed**.

### Resolution and validation — Completed 2026-10-08

Both EF reader queries now apply `.OrderBy(e => e.Position)` before `.AsAsyncEnumerable()`. Global and stream reads return events in ascending position order, allowing polling to advance past the greatest emitted position without redelivering the previous batch.

Ran `dotnet test "src/EventSourcing.Test/EventSourcing.Test.csproj"`: **12 passed, 0 failed**, including all five ordering regression cases and the seven existing tests.

## 3. Resolved — SQL Server writer serialization

The native `SqlServerEventWriter` applies `TABLOCKX` directly to every Events insert and holds the exclusive table lock through commit or rollback. SQL rewriting and EF-generated write statements are no longer involved. Scalar and TVP appends use one atomic autocommit INSERT; staged bulk appends own a client transaction.

`SqlServerEventStoreTest.WriterWaitsUntilEarlierBatchCommitsOrRollsBackAndPollingCrossesGaps` observes actual Events table-lock waiters and verifies commit, rollback, and polling across identity gaps for all three transports. Snapshot readers can see prior committed state while a writer holds the lock, but another writer cannot publish higher positions ahead of it. Within-batch ordering is covered in finding 10.

## 4. Documentation — Clarify startup and replay readiness

### Locations

- `src/EventSourcing.Commands/Infrastructure/Internal/FunicularCommandsInitializer.cs:17–22,30–34`
- `src/EventSourcing/EventSourcingContext.cs:21–33`

### Clarified contract

Returning before replay finishes is intentional when no after-replay initializer is registered. `StartEventSourcing()` calls the context's parameterless `Initialize()` independently, but its per-phase hook runs only for phases represented by registered initializers.

Registering an `IAfterEventReplayInitializer` (or `IInitializer<AfterEventReplay>`) makes startup await replay before running that initializer. `StartEventSourcing()` then waits for all registered initializers to complete. Without such a registration, the command initializer schedules the no-op marker and startup can return while replay continues.

Applications requiring replayed state must explicitly await `IEventReplayState.WaitForReplayDone()` after starting event sourcing and before enabling command/query access. Starting event sourcing later only delays replay; starting and readiness can instead be separate milestones, for example during a long replay in a Windows service.

### Documentation resolution — 2026-10-08

Documented both registration-dependent startup behavior and explicit replay waiting in `README.md`, including a readiness example and the additional barrier required by asynchronously processing projections. No unconditional startup barrier is required under this contract.

The Meetup application now explicitly awaits `IEventReplayState.WaitForReplayDone()` after `StartEventSourcing()` and before `app.RunAsync()` in `src/Playground/Meetup/Meetup/Program.cs`. The example's replay-readiness gate is implemented.

## 5. Reassessed — Existing startup validation; runtime command cleanup remains open

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

### Reassessment — Redundant startup check reverted 2026-10-08

The normal polling configuration already resolves `IEventStore` via `GetRequiredService` in `PollingEventStreamOptionsExtension.BuildPollingEventStream`. Startup constructs that stream through `EventStreamStarter`, so missing registration or construction failure already fails startup independently of the commands package. The proposed command-specific early check was added briefly and then reverted as redundant for this configuration. No additional startup validation is required under the agreed scope; custom stream configurations that do not resolve `IEventStore` are not covered by this existing check.

Reclassify the startup-validation portion as addressed by existing behavior rather than a general startup defect. Runtime per-command scope cleanup and subscription terminal-error handling remain open: store resolution still occurs outside the per-command `try/finally`, so an unexpected runtime construction failure can leak the command scope and terminate the subscription.

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

### Resolution and validation — Completed 2026-10-08

Added `.Distinct()` before constructing the factory dictionary in `EventFactory.WarmupFactoryCache`. Multiple persisted versions can now share one domain payload type and its typed event factory, while mapper selection remains keyed by distinct persisted event types.

Added `src/EventSourcing.Test/MapperVersionCompatibilityTest.cs`. The compatibility regression initially failed during `StartEventSourcing()` with a duplicate domain-type key. With the fix, startup succeeds and reading two stored JSON versions produces `Event<VersionedEntry>` instances with correctly mapped text, stream identity, ordered positions, and preserved timestamps. A separate regression confirms duplicate persisted event-type registrations still throw `ArgumentException`.

Ran `dotnet test "src/EventSourcing.Test/EventSourcing.Test.csproj"`: **22 passed, 0 failed**.

## 7. Resolved — Global-namespace command extension classes

### Location

`src/EventSourcing.Commands.SourceGenerator/Generator.cs:38,53–54,98–118,145`

### Incorrect behavior

Global-namespace command extension classes produce invalid namespace declarations in the generated templates. This is independent of the result type's namespace: a global-namespace extension class referring to a supported top-level result in a named namespace is affected too. Ordinary and nested namespaces are already extracted from Roslyn symbols.

Nested result types are outside the agreed support scope; source inspection of FunicularSwitch's current `main` result generator shows it does not recreate containing type declarations. This is not a version-specific compilation verification of the referenced 4.6.0 package. Nested command extension classes are also outside scope, since C# extension methods require a top-level static class.

### Proposed regression tests

Use `CSharpGeneratorDriver` compilation fixtures for:

1. Error-only attributed extension classes in the global namespace.
2. Global-namespace extension classes referring to top-level result/error types in named namespaces.
3. Existing top-level generation paths, different namespaces, nested namespace syntax, multiple attributed classes, and unmatched attributes.

Assert error-free resulting compilations and that generated methods belong to the intended types.

### Proposed fix

Omit the template namespace wrapper for global-namespace command extension classes. Preserve existing named-namespace output and add compilation plus generated-source snapshot coverage in the style of FunicularSwitch's generator specs. Nested-type support and new unsupported-declaration diagnostics are outside this change.

### Resolution and validation — Completed 2026-10-08

The generator explicitly detects the global namespace and uses Roslyn syntax to lift the embedded template's declarations out of its namespace wrapper, preserving `#nullable enable`. Global-namespace source hint names no longer start with a dot. Named-namespace generation retains its existing template replacement path.

Added `src/EventSourcing.Commands.SourceGenerator.Test` to the solution. Its MSTest specs follow FunicularSwitch's inline-source and Verify snapshot style. The helper runs `CSharpGeneratorDriver` against the actual Commands/core/Rx assemblies, rejects generator exceptions and error diagnostics, checks the expected generated-source count, and requires the updated consumer compilation to have no errors before snapshot verification. Snapshot baselines are checked in and excluded from compilation; normal tests compare snapshots rather than automatically approving changes.

Eight cases cover:

- Error-only generation, including async/synchronous processor bases and processing-result helpers.
- Partial result augmentation with `IResult` and a non-partial result with an existing implementation.
- Different extension/result/error namespaces and block-scoped nested namespace declarations.
- Global-namespace error-only extensions and global-namespace extensions with a named partial result.
- Multiple attributed classes with the same name in distinct namespaces.
- No matching attribute producing no source.

Consumer fixtures call typed/untyped command-wait helpers, timeout overloads, result-wait helpers, and result conversion, so the tests verify usable generated APIs as well as source shape. Results are self-contained fixtures; these tests do not run the upstream FunicularSwitch result generator.

Supported-output baseline verification: the initial pre-fix run covered only the two global-namespace regressions; supported-case snapshots were first created after the fix. Subsequently restored the original generator logic temporarily (verified against Git's original version; only whitespace differed) and ran `dotnet test "src/EventSourcing.Commands.SourceGenerator.Test/EventSourcing.Commands.SourceGenerator.Test.csproj" --filter "FullyQualifiedName!~global_namespace"`: **6 passed, 0 failed**, comparing against the same snapshots without updating them. This verifies unchanged source content and hint names for the covered supported cases, plus no output for unmatched attributes. Restored the fix and reran the entire generator project: **8 passed, 0 failed**. The snapshots were unchanged throughout this baseline comparison.

Both global-namespace cases first failed with invalid namespace declarations (`CS1001`), reproducing the defect. After the fix, ran `dotnet test "src/EventSourcing.Commands.SourceGenerator.Test/EventSourcing.Commands.SourceGenerator.Test.csproj"`: **8 passed, 0 failed**. Ran `dotnet test "src/EventSourcing.Test/EventSourcing.Test.csproj" --filter "TestCategory!=SqlServerIntegration"`: **30 passed, 0 failed**. The Meetup project built with **0 warnings, 0 errors**, exercising its existing FunicularSwitch integration. `git diff --check` passed.

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

## 9. Resolved — Caller-supplied command wait timeout

### Locations

- `src/EventSourcing.Commands/Infrastructure/Internal/CommandRegistrationExtensions.cs:53–66`
- `src/EventSourcing.Commands/ICommandBus.cs:20–28`

### Incorrect behavior

If both the original write and the fault-marker write fail, the command is logged and discarded. Its waiter never receives a completion event or the terminal persistence failure. The wait API uses `CancellationToken.None` and leaves its subscription active.

An external `Task.WaitAsync(timeout)` stops the caller's wait but does not cancel the internal subscription.

### Proposed regression test

Use a store that fails both writes. Assert the wait API terminates with an explicit persistence failure, or can be cancelled through the API. Verify subscription cleanup, then recover the store and confirm a subsequent command completes normally.

### Agreed contract and resolution — Completed 2026-10-08

A caller-supplied timeout is sufficient under the clarified contract. Added `TimeSpan timeout` overloads for typed and untyped `SendAndWaitForProcessedEvent`, and matching generated helpers including `SendCommandAndWaitUntilApplied`. Existing overloads retain indefinite waiting for compatibility. A deadline bounds both sending and waiting, throws `TimeoutException`, and releases the event subscription before the call returns. The existing scheduled Rx `ToTask` bridge is retained, with `ConfigureAwait(false)` on the awaits. Timed waits use a single deadline token for both the waits and Rx subscription. In `finally`, cancellation is requested and the Rx task is awaited to ensure unsubscription has finished, including when timer cancellation is concurrently underway. Cleanup observes task errors without replacing the original result or send/wait exception.

A timeout ends the caller's wait, not command execution. It does not establish whether persistence succeeded, and the command can complete later, so retrying is not automatically safe. No terminal persistence-failure channel is required for this agreed resolution. The command layer's logging/fault-marker behavior is unchanged. The public overloads describe the timeout contract in XML comments; no timeout documentation in `README.md` is required under the agreed scope.

Added eight cases in `src/EventSourcing.Test/CommandWaitTimeoutTest.cs` covering typed/untyped streams, completion during send, filtering unrelated command IDs, timeout cleanup followed by a successful independent wait, timed send-failure cleanup, and timeout during an incomplete send without cancelling that send. The initial two non-pumping-context cases passed even without `Scheduler.Default`; they did not protect the scheduler workaround and were replaced with the publisher/continuation regression below. These are focused wait-API tests, not an end-to-end double-persistence-failure reproduction.

Per the subsequent simplification, no cancellation source is allocated when no timeout is supplied. That path uses `ToTask(CancellationToken.None, Scheduler.Default)` and retains the original Rx lifecycle: completion or stream errors release the subscription internally, while exceptional send failures no longer trigger explicit subscription cleanup. Timed waits now use only the deadline cancellation source, with cleanup awaiting the scheduled Rx task rather than allocating a separate subscription cancellation source.

An initial shared cancellation token exposed a cleanup race during an incomplete send. A replacement using `ObserveOn` and a manual task-completion source passed the functional tests but changed scheduling semantics. After the historical mixed sync/async deadlocks were raised, that replacement was removed and the original scheduled `ToTask` conversion was restored. Separate deadline/subscription sources temporarily addressed cleanup, but were subsequently replaced by the single-source implementation described above. Awaiting the Rx task in `finally` ensures unsubscription completes even when deadline cancellation is already underway.

Validation after restoring the scheduled task bridge: **31 non-integration tests passed, 0 failed**, including all nine wait-API cases. The prior template build passed with **0 warnings, 0 errors**, and the Meetup project also built successfully.

### Final single-source cleanup validation — 2026-10-08

The no-timeout send-failure cleanup test was removed to retain the original no-timeout lifecycle, leaving eight wait-API cases. With the single deadline source but no `finally` block, four cases failed: typed and untyped timeout cleanup, timed send-failure cleanup, and timeout during an incomplete send. Both scheduler-sensitive regressions still passed. Restoring `finally` to cancel the deadline and await the Rx task resolved all four failures without a separate subscription cancellation source.

Ran `dotnet test "src/EventSourcing.Test/EventSourcing.Test.csproj" --filter "FullyQualifiedName~CommandWaitTimeoutTest"`: **8 passed, 0 failed**. `git diff --check` passed. The earlier full-suite and build results above precede this final simplification; the final single-source implementation has focused wait-API validation.

### Scheduler-sensitive deadlock regression — 2026-10-08

`CallerContinuationCanWaitForPublisherToReturnFromOnNext` covers timeout and no-timeout overloads. A dedicated publisher thread emits the matching event. A caller continuation registered with `ExecuteSynchronously` synchronously waits for the publisher to return from `OnNext`. Without scheduled task completion, the continuation runs inline inside `OnNext`, creating a circular wait: the publisher cannot return until the continuation returns, and the continuation waits for the publisher. A two-second escape timeout bounds the reproduction and reports failure rather than hanging the test runner; thread cleanup is also bounded.

Mutation validation: temporarily changed only `.ToTask(subscriptionCancellation.Token, Scheduler.Default)` to `.ToTask(subscriptionCancellation.Token)`. **Both cases failed**, each after the two-second escape timeout, because the publisher could not return while the continuation was running. Restored `Scheduler.Default`; **all 31 non-integration tests passed**. The successful cases additionally verify that the caller continuation runs on a different thread from the publisher. This reproduces a concrete inline-continuation deadlock mechanism protected by the scheduler; it is not claimed to identify the original historical incident.

## 10. Resolved — SQL Server batch ordering

The native writer uses `INSERT ... SELECT ... ORDER BY InputOrdinal` for scalar, TVP, and staged-bulk inputs. The explicit source order defines identity computation order, independently of physical row insertion order, as documented in [SQL Server INSERT](https://learn.microsoft.com/en-us/sql/t-sql/statements/insert-transact-sql?view=sql-server-ver17).

`SqlServerBatchOrderingTest` verifies input order and an actual `CommandProcessed<string>` marker at the final position across all three transports, with varying stream IDs and payload widths. The opt-in tests use `TEST_SQLSERVER_CONNECTION_STRING`, create isolated databases with the production migration, and delete them after execution. Writer serialization remains a separate guarantee, covered by finding 3.

## Existing test weakness — Subscription occurs after writing

### Location

`src/EventSourcing.Test/HandleBadCasesTest.cs:116–125`

### Incorrect behavior

The helper writes before subscribing to the hot stream. The poller can publish in between, causing a timeout even when production behavior is correct.

### Proposed regression test and fix

Use a writer that waits until the poller has delivered an event before returning to expose the race deterministically. Create the observable subscription/task before writing, and cancel/dispose it if writing fails.

The test project now references both Commands and the SQL Server provider and contains the additional regression coverage recorded above. Those original coverage prerequisites are satisfied; the subscribe-after-write race in this helper remains open.

## Implementation order

Completed: failed-save isolation and serialization-failure cleanup (1/1a), explicit read ordering (2), SQL Server writer serialization and within-batch ordering (3/10), startup/readiness documentation and the Meetup replay gate (4), mapper-version compatibility (6), global-namespace command extension generation and supported-path regression coverage (7), and caller-supplied wait timeout/cleanup (9). Normal polling startup already validates the event store (5).

Remaining work, in suggested order:

1. Fix runtime per-command scope cleanup and shared subscription terminal-error handling (5).
2. Advance checkpoints independently of successfully mapped events (8).
3. Fix the existing subscribe-after-write test race.
4. Design and validate an event-write retry policy (1b), including ambiguous commit recovery and duplicate prevention. Failed-save isolation is complete; retry behavior decisions remain required.

Use deterministic tests with interceptors, controlled readers, and synchronization barriers. SQL Server commit-order and batch-order behavior require actual SQL Server integration coverage; SQLite cannot validate those guarantees.
