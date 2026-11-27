# Diff package improvement plan

Status: proposed improvements, not implementation commitments.

Scope: evolve `Bluehands.EventSourcing.Diff` independently of ScriptRunner. Prioritize predictable collection semantics and correctness over adding options. Keep ordered comparisons as the default and settle collection behavior and difference kinds before a stable release.

## 1. Unordered comparison for atomic elements

Add opt-in unordered comparison per collection property/path.

- Compare as a multiset: ignore order but preserve duplicate counts.
- Allow an `IEqualityComparer<T>`, such as `StringComparer.OrdinalIgnoreCase`.
- Report one whole-collection difference, consistent with current atomic-collection behavior.
- Do not require elements to implement `IComparable`.
- Use equality-based counting rather than sorting. With a well-behaved hash comparer, comparison is generally O(n).

Expected behavior:

```text
[a, b, a] vs [b, a, a] -> equal
[a, b, a] vs [b, a]    -> different
```

Acceptance checks:

- Reordering alone produces no difference.
- Duplicate-count changes produce a difference.
- Empty collections, null elements, null collection transitions, and custom equality are covered.
- Existing ordered comparisons retain their behavior.
- Explicit custom element comparisons are not silently bypassed; define their interaction with unordered matching before implementation.

## 2. Key-based matching for complex collections

Treat identity-based matching as a separate feature from unordered atomic equality.

- Require explicit key selection, such as `Customer.Id`; do not infer identity from a property named `Id`.
- Match objects by key, then recursively compare matched objects.
- Use stable keyed paths, such as `Customers[customerId].Address.City`.
- Report additions and removals without producing positional changes for subsequent items.
- Reject duplicate keys rather than choosing an arbitrary match.
- Define key equality, invalid/null-key handling, and result ordering explicitly.

Acceptance checks:

- Reordering produces no differences.
- An insertion or removal affects only that key.
- A matched object's property change uses its keyed path.
- Duplicate keys are rejected with an actionable error.
- Path exclusions work for added, removed, and modified entries.

## 3. Explicit difference kinds

Add a small `DifferenceKind` with:

- `Added`
- `Removed`
- `Changed`

Added/removed describe collection membership, not merely a property transitioning to or from null. A property null transition remains a value change.

Rationale: currently, adding or removing a dictionary entry whose value is null can produce the same `OldValue = null` and `NewValue = null`. An explicit kind removes this ambiguity without introducing a patch engine.

Acceptance checks:

- Added and removed null-valued entries are distinguishable.
- Dictionary and complex-collection changes assign consistent kinds.
- Scalar changes, null transitions, and runtime-type changes have documented kinds.
- Update generated code, runtime helpers, custom-comparison examples, tests, and package documentation together.
- Decide constructor compatibility before changing the public result type.

## 4. Diagnostics and dictionary hardening

### Ignored property names

Warn when a configured ignored property does not exist. Today a typo silently does nothing. Validate against the applicable model hierarchy and interfaces, retaining support for inherited exclusions.

### Dictionary interfaces

Support `IDictionary<TKey, TValue>` as keyed data, or issue an actionable diagnostic. Currently that interface alone falls through to ordered enumerable comparison. Avoid ambiguity for types implementing both mutable and read-only dictionary interfaces.

### Dictionary key equality

Provide an explicit, consistent matching policy. Current lookups use each dictionary's comparer, so mismatched comparers can produce asymmetric results. Cover case-sensitive versus case-insensitive dictionaries and swapping old/new inputs. Do not silently choose conflicting equality semantics.

### Custom comparisons and ignored paths

Ensure generated call sites consistently respect ignored paths before invoking custom code. Direct calls to user-written comparison methods remain that method's responsibility. Cover custom comparisons at property, dictionary-value, and collection-element paths.

### Unsupported model shapes

Add regression tests for conflicting inherited interface properties and mixed nullable generic usages. Support them correctly or produce actionable generator diagnostics rather than generated-code compiler errors. These edge cases require reproduction before being classified as confirmed defects.

## 5. Measure allocations before optimizing

The current implementation builds paths and intermediate immutable result arrays throughout recursion. Measure their cost before changing the design.

Benchmark scenarios:

- Identical/shared state.
- Equal but separately allocated state.
- One deeply nested change.
- Many dictionary changes.
- Large reordered collections, once unordered matching is available.

If allocations are significant, first evaluate one internal result accumulator while retaining an immutable public result. Preserve paths, ordering, exclusions, custom comparison behavior, and error handling. Do not introduce pooling or elaborate caching without evidence.

## Recommended delivery order

1. Add atomic unordered comparison, explicit difference kinds, and diagnostic/dictionary hardening. Resolve the public contracts first, then implement each change with focused regression tests.
2. Add key-based complex matching when entity-list consumers justify it.
3. Measure runtime and allocation costs; optimize only demonstrated bottlenecks.

For each increment, run the focused tests, build the solution, and verify the packed package in the existing multi-assembly consumer smoke check. Keep technical documentation in `src/EventSourcing.Diff/README.md`.

## Deferred work

- Glob syntax: exact paths already work; patterns need a carefully defined grammar.
- General graph support: if needed, start with fail-fast cycle detection rather than automatic graph matching. Detection must not suppress valid differences at multiple paths to shared objects.
- Patch application and serialization.
- Automatic identity discovery.
- Automatic secret detection/redaction.
- Broader target-framework support without a concrete consumer requirement.

## Evidence and limits

This plan is based on source inspection and the initial implementation's documented behavior. No new performance measurements or reproductions of every listed edge case were performed when proposing it. Preserve existing behavior unless a requirement explicitly changes it, and document intentional API or semantic changes.
