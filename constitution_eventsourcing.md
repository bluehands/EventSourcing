### Principle: Events are the source of truth
- Every change to domain state owned by this system MUST be stored as an event. The event log only grows: domain events are never changed or deleted.
- Events are named as domain facts in the past tense (`AttendeeRegistered`).
- Events are immutable records. They contain only the data that describes the fact, including their stream id.
- Event type identifiers are stable constants. The stored payload format MUST stay backward compatible. Keep domain events separate from stored payloads by using payload mappers.

### Principle: Commands are functions from intent to events
- A command is an immutable record that states an intent in the imperative (`RegisterAttendee`).
- Command handling is basically a function: `(state, command) → Result<events, error>`. It need not be pure: besides projection state, it may read other state and call services.
- Command handlers run once and are never replayed, so they do not need to be idempotent or reproducible. Everything that projections depend on, such as ids, timestamps or data returned by services, is decided once and stored in the events. It is never recalculated later.
- Handlers may cause side effects, but must accept that appending the events can still fail afterwards.
- The `Result` returned by a handler is only for the caller, for example when the caller waits for a projection. It saves you from modelling every validation failure or technical error as an event. Projections MUST NOT build domain state from command results. Only failures that matter to the domain are modelled as domain events.
- Command results are stored as `CommandProcessed` events only so that the framework can return the `Result` to the waiting caller (`SendCommandAndWaitUntilApplied`). They carry no domain state and MAY be deleted after a retention period.
- Application code, queries, reactions and projections MUST NOT read, subscribe to or query `CommandProcessed` events. To find out the effect of a command, read the projections it affected. The `Result` only says whether the command succeeded or failed. Ids, statuses and other outcomes come from projection state.
- All events produced by one command are appended atomically and in order.

### Principle: Projection state is immutable and dependency-free
- Projection state uses only immutable records and immutable collections. It MUST NOT hold services, I/O, framework types or infrastructure references. It must be possible to create and test it with no setup.
- Each state change is a pure function: `(state, event) → state'`. Given the same input, it always gives the same result.
- That function must handle every event without throwing. Events it does not care about leave the state unchanged.
- Projections may use event data and event metadata such as position and timestamp. They MUST NOT use the current time, random values or outside lookups.
- Replay MUST be safe: rebuilding a projection from the log always gives the same state.
- Domain behavior should live on the state records, for example `talk.AttendeeRegistered(e)`. Subscriptions, holding the current state and change notifications belong in a thin hosting wrapper that is separate from the state types.
- Projections can be thrown away and rebuilt by replaying the log. Each projection is shaped for the code that reads it.

### Principle: Strict read/write separation
- Queries never call command handlers.
- Side effects triggered by events MUST NOT be repeated on replay. Reactions either track how far they have processed or are idempotent.
- Read models stored outside memory are updated idempotently per event position.
- The API stays unavailable until replay has finished.
- When a caller needs to read its own write, it waits until the command has been applied (for example with `SendCommandAndWaitUntilApplied`) and then reads the affected projection. It never reads the store or `CommandProcessed` events directly.

### Principle: Concurrency is a domain decision
- The event store allows conflicting events. No concurrency pattern is applied automatically.
- Command handlers check against projection state that may be out of date. A successful check does not guarantee that no conflicting event will be stored.
- Projections detect conflicts and either resolve them deterministically or make them visible. For example, a registration over capacity puts the attendee on the waitlist.
- How each conflict is handled is decided per domain case, for example by including the version the command was based on in critical events. That decision should be recorded in the feature spec.
