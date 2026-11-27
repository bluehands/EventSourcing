# Diff package improvements: done

Completed parts of the original improvement plan. Open items are in `../open/diff-improvements.md`.

## Sets compared by membership

Part of the original "unordered comparison" item. `ISet<T>`/`IReadOnlySet<T>` properties are compared with `SetEquals`; differing elements are reported as `Added`/`Removed` at the set path. Set elements are not traversed. Implemented in `diff-fixes.md`, step 3.

## Key-based matching for complex collections

Done via `[Diff(typeof(T), MatchBy = nameof(T.Id))]` (`ESDIFF006`/`ESDIFF007`, `CollectionDiff.Keyed`). Implemented in `diff-fixes.md`, step 4.

- Explicit key selection per element type, also applied to subclasses and implementations.
- Matched objects are compared recursively with keyed paths such as `Customers[customerId].Address.City`.
- Additions and removals affect only their key; reordering produces no differences.
- Duplicate keys, null keys and null elements throw `InvalidOperationException` naming the path.
- Path exclusions work for added, removed and modified entries.

## Explicit difference kinds

Done: `DifferenceKind { Changed, Added, Removed }`, `Difference.Kind` (defaults to `Changed`) and the factories `Difference.Changed/Added/Removed`. Added and removed null-valued dictionary entries are now distinguishable. Implemented in `diff-fixes.md`, step 1.

## Diagnostics and dictionary hardening (done parts)

- **Ignored property names:** `ESDIFF005` warns when an ignored name matches no public instance property of the type, its base types or interfaces.
- **Dictionary interfaces:** `IDictionary<TKey, TValue>` is matched by key like `IReadOnlyDictionary<TKey, TValue>`; types implementing both use the read-only interface.
- **Conflicting interface properties:** `ESDIFF004` reports ambiguous inherited properties; ignoring the property resolves it, and `new` redeclarations are not ambiguous.
- **Custom comparisons and ignored paths:** generated call sites check ignored paths before invoking custom code (`DiffCollector.Enter`), for properties, dictionary values and collection elements. Implemented in `diff-runtime-performance.md`.

## Measure allocations before optimizing

Done and replaced by `diff-runtime-performance.md`: benchmark project `src/EventSourcing.Diff.Benchmarks`, allocation-free comparisons for unchanged states, and `HasDifferences`.
