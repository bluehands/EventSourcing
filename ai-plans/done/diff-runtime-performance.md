# Diff runtime performance and `HasDifferences`

Status: done. Measured results (see `src/EventSourcing.Diff.Benchmarks/README.md`): unchanged states with shared substructure went from ~24 KB / 5.7 µs to 0 B (24 B benchmark boxing) / ~70 ns; one deep change from 26 KB to 416 B; `HasDifferences` allocates 0 B except for non-identical keyed lists (~10 KB). Replaces item 5 of the original diff improvement plan (`diff-improvements.md` in this folder). The open follow-up for keyed lists is item 5 in `../open/diff-improvements.md`.

## Why

Generated comparisons run in the hot path during event replay, roughly 100,000 to 1,000,000 calls at startup. Source inspection shows the cost is mostly allocation, and most of it happens even when nothing changed:

- Generated object methods call `path.Push("Prop")` for every property before the child checks anything. Each push allocates a segment record, a copied `ImmutableArray` (its cost grows with depth) and a `PropertyPath`.
- Every object comparison creates an `ImmutableArray` builder, even when there's no difference.
- Every property, including simple values such as `int` and `string`, goes through a method call plus an empty `AddRange`.
- `PathsToIgnore.Contains(path)` hashes the whole path at every node.
- `CollectionDiff.Enumerable` boxes enumerators and pushes an index path for every element. `Keyed` builds two dictionaries and boxes value-type keys on every call.

Rough estimate, not measured: 60–80 allocations per call for an unchanged state with 20 properties and 3 levels of nesting. That is several GB of garbage per startup.

Callers that only need to know *whether* something changed currently pay for the full list of differences. `HasDifferences` fixes this.

## Goals

- **Zero allocations** when the compared values have no differences, at least for objects, simple values and positional collections, with no ignore list.
- **Allocations proportional to the number of differences** otherwise.
- **A generated `HasDifferences`** that stops at the first difference.
- **Unchanged results:** paths, kinds, ordering, ignore semantics, custom comparisons and exceptions stay as they are. The existing 54 tests and the package smoke check stay green with no changed assertions.

## Steps

### 1. Benchmark baseline (done)

- Add `src/EventSourcing.Diff.Benchmarks` (BenchmarkDotNet, `[MemoryDiagnoser]`, not packable, added to `EventSourcing.slnx`). Keep it separate from the event-store workload runner in `src/EventSourcing.Benchmarks`.
- **Model:** a replay-like state of about 20 properties and 3 levels. It contains:
  - simple values: `int`, `string`, `Guid`, `DateTimeOffset`, an enum
  - a nested record
  - a polymorphic property
  - an `ImmutableArray` of records
  - an `ImmutableDictionary`
  - a `MatchBy` list of 50 elements
- **Scenarios:**
  - same reference
  - unchanged but with shared substructure (a `with` copy that changes nothing)
  - equal but fully separate instances
  - one change deep in the tree
  - one change in a keyed element
  - many dictionary changes
  - each of these with a non-empty `PathsToIgnore`
- Record the baseline (time and bytes per call) in `src/EventSourcing.Diff.Benchmarks/README.md`. Update that file after every step below.

### 2. `HasDifferences` API (decide first, because it shapes steps 3–6) (done)

Decision: one internal "sink" drives both modes, so that no collection algorithm exists twice.

- **Runtime:** add a public `DiffCollector` class, marked `[EditorBrowsable(Never)]` and documented as being for generated code. It holds the settings, a lazily created list of differences, `bool StopAtFirst` and `bool Stopped`, and the current path as a segment stack (see step 3). The collection algorithms (dictionary, set, keyed, enumerable) move onto the collector as public, generated-code-only methods. The existing public `CollectionDiff` signatures stay as thin wrappers that create a collector and run the same code.
- **Walk delegate:** `public delegate void DiffWalk<in T>(T oldValue, T newValue, DiffCollector collector);` used by generated code for element and value comparisons. Static method groups are cached by the compiler, so passing them doesn't allocate.
- **Generator**, for each type `T` it emits:
  - `public static ImmutableArray<Difference> Diff(T? oldValue, T? newValue, PropertyPath? path = null, DiffSettings? settings = null)`, unchanged signature
  - `public static bool HasDifferences(T? oldValue, T? newValue, DiffSettings? settings = null)`
  - a private `CollectDifferences(T? oldValue, T? newValue, DiffCollector collector)` that does the work
- **Custom comparisons:** keep the existing signature. Generated code calls them with the materialized current path and the settings, adds their result, and in stop mode stops if the result isn't empty. Custom comparisons are always called (no reference-equality shortcut), exactly as today. Optional later: a custom `bool HasDifferences(T, T, DiffSettings?)` overload that is used when present.
- **Contract:** `HasDifferences(a, b, s) == !Diff(a, b, null, s).IsEmpty` for every input. One exception: stopping early may skip invalid keyed data (duplicate or null keys) further on in the tree. Document this.
- **Extension methods (decided):** `Diff` and `HasDifferences` are extension methods (`this T? oldValue`) for types named in a `[Diff(typeof(T))]` attribute, so `oldState.HasDifferences(newState)` works. Methods generated only for types reached through properties (including `string`, `int` and collections) stay plain static methods so that they don't clutter IntelliSense on common types. `Comparisons.Diff(a, b)` keeps working.

### 3. Path stack in the collector and inline checks in generated code (done)

Paths are only needed when a difference is recorded, a custom comparison is called, or an ignore rule might apply. So the collector tracks the current location as a stack of segments (property name, key or index in a reusable array) instead of allocating a `PropertyPath` per step. It builds a `PropertyPath` only when recording a difference or calling a custom comparison (start path given to `Diff` + stack).

- **Ignore rules without paths:** `DiffSettings` lazily builds (and caches) a trie of its `PathsToIgnore`. The collector tracks the current trie node per stack level. Entering a segment that ends an ignored path returns false and skips the subtree. Once there is no trie node, no ignore rule can apply below and the check is skipped. The trie is positioned along the start path given to `Diff`.
- **Generated object methods**, per property instead of `differences.AddRange(Diff(o.P, n.P, path.Push("P"), settings))`:
  - simple values without a custom comparison: `if (!EqualityComparer<TP>.Default.Equals(o.P, n.P)) collector.Changed("P", o.P, n.P);` (checks the ignore rule for `P`, records the difference)
  - other children: `if (!ReferenceEquals(o.P, n.P) && collector.Enter("P")) { CollectDifferences(o.P, n.P, collector); collector.Exit(); }` (no reference check for value types or types with custom comparisons). This skips whole unchanged subtrees, which is common because replay states reuse unchanged parts.
  - after each property: `if (collector.Stopped) return;`
- **Equal values never check ignore rules.** This is harmless, because equal values produce no differences either way.

### 4. Cheaper `PropertyPath` (done)

- Store a path as a parent pointer plus one segment, with a cached depth and hash. `Push` becomes a single allocation with no array copy, which makes materializing paths from the collector stack cheap.
- Merge the segment into the node. `PathSegment` records are still exposed through `Segments`, which is built on demand.
- Equality compares depth and hash first, then walks the parent chain.
- Public API, `ToString`, equality and hash semantics stay the same (equal paths still compare equal).

### 5. Result building (done)

- The collector creates its list on the first difference only. With no differences, `Diff` returns `ImmutableArray<Difference>.Empty`.
- One collector per root call, instead of one builder plus one `ToImmutable` per object level. Root calls rent the collector from a `[ThreadStatic]` cache and reset it afterwards; nested root calls (e.g. from a custom comparison) fall back to a new instance.

### 6. Collections (done)

- Use index loops for arrays, `ImmutableArray<T>`, `List<T>` and `IReadOnlyList<T>`. Keep `IEnumerable<T>` as the fallback. The generator picks the overload from the detected type.
- Before comparing elements in `Enumerable` and `Keyed`, skip pairs that are the same reference.
- **`Keyed` fast path:** if both sides have the same length and the same element references in the same order, return without building dictionaries. Validation then only happens when the elements differ; document this.
- In `Keyed`, keep both key lookups (needed to detect duplicate keys on either side). Keys and indices go onto the collector stack, so no path is built per element. Value-type keys are boxed only for elements that are compared.
- `Dictionary`: when `IReadOnlyDictionary` is the concrete `ImmutableDictionary` or `Dictionary`, use the struct enumerators so they aren't boxed.

### 7. Docs (done)

- **`src/EventSourcing.Diff/README.md`:** document `HasDifferences` and its contract, the extension-method usage (`old.Diff(new)`, `old.HasDifferences(new)`), plus the performance notes "unchanged parts that are the same reference are skipped" and "paths are built only for differences".
- **`ai-plans/diff-improvements.md`** (now `done/diff-improvements.md`): mark item 5 as replaced by this plan.
- **`Test-Package.ps1`:** extend it with one `HasDifferences` check.

## Tests

- **Equivalence:** for every existing generator and runtime scenario, assert `HasDifferences == !Diff(...).IsEmpty`. Do this with a test helper rather than separate tests.
- **Extension methods:** `old.Diff(new)` and `old.HasDifferences(new)` compile for configured types, including polymorphic roots and nullable structs. Generated methods for types reached only through properties (e.g. `string`) are not extensions.
- **New `HasDifferences` tests:**
  - stopping early, using a custom comparison that counts its calls and must not be called after the first difference
  - ignored paths
  - null and runtime-type transitions
  - keyed, set and dictionary properties
- **Allocation test:** `GC.GetAllocatedBytesForCurrentThread()` must report 0 bytes for `HasDifferences` and `Diff` on unchanged states with shared substructure and on equal, separately built states (positional collections, no keyed lists), both with and without an ignore list. Warm up first.

## Acceptance

- All existing tests pass with no changed assertions; the new tests pass.
- The Release build has 0 warnings, and the pack and smoke check pass.
- The benchmark README shows the baseline next to the result of each step. The goals above are met or the gap is explained.

## Risks

- **Bigger public API surface** (`DiffCollector`, `HasDifferences`). Mitigation: nothing is published yet; hide `DiffCollector` from IntelliSense and document it as generated-code-only.
- **Generated code gets more complex.** Mitigation: the equivalence test helper covers every scenario.
- **The inline-equality fast path skips `PathsToIgnore` checks for equal values.** This is harmless, because equal values produce no differences either way.
- **Ignore trie and path stack must match today's ignore semantics exactly** (an exact path stops the comparison at that node and below, including for keys and indices, and a start path passed to `Diff` counts). The existing ignore tests plus the equivalence helper guard this.
