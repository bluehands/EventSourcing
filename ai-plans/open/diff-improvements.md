# Diff package improvements: open

Status: open. Completed items are in `../done/diff-improvements.md`.

Scope: evolve `Bluehands.EventSourcing.Diff`. Prioritize predictable collection semantics and correctness over adding options. Keep ordered comparisons as the default.

## 1. Unordered comparison for atomic lists

Sets are already compared by membership. Still open: opt-in unordered comparison for lists per collection property/path.

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
- `HasDifferences` stays equivalent to `!Diff(...).IsEmpty`, and unchanged lists stay allocation-free where possible.

## 2. Dictionary key equality

Provide an explicit, consistent matching policy. Current lookups use each dictionary's comparer, so mismatched comparers can produce asymmetric results. Cover case-sensitive versus case-insensitive dictionaries and swapping old/new inputs. Do not silently choose conflicting equality semantics.

## 3. Test coverage for custom comparisons at ignored paths

Generated call sites already skip ignored paths before invoking custom code. Add explicit regression tests for custom comparisons at property, dictionary-value and collection-element paths.

## 4. Mixed nullable generic usages

Add regression tests for models using the same generic type with different nullable annotations (e.g. `Box<string>` and `Box<string?>`, `List<T>` and `List<T?>`). Support them correctly or produce actionable generator diagnostics rather than generated-code compiler errors. Reproduce before classifying as a defect.

## 5. Keyed lists without dictionaries for equal states

Equal but separately built keyed lists still build two key lookups (~10 KB per call for 50 elements, see `src/EventSourcing.Diff.Benchmarks/README.md`). Possible follow-up: when both sides are lists of the same length, first try matching elements by position (same key at the same index) and fall back to the lookups only when the order or keys differ. Duplicate-key validation must stay defined (document if it is skipped, as for identical lists). Do this only if replay states rebuild keyed lists often.

## Delivery notes

For each increment, run the focused tests, build the solution, and verify the packed package in the existing multi-assembly consumer smoke check. Keep technical documentation in `src/EventSourcing.Diff/README.md`.

## Deferred work

- Glob syntax: exact paths already work; patterns need a carefully defined grammar.
- General graph support: if needed, start with fail-fast cycle detection rather than automatic graph matching. Detection must not suppress valid differences at multiple paths to shared objects.
- Patch application and serialization.
- Automatic identity discovery.
- Automatic secret detection/redaction.
- Broader target-framework support without a concrete consumer requirement.

## Evidence and limits

This plan is based on source inspection and the implementation's documented behavior. Not every listed edge case has been reproduced. Preserve existing behavior unless a requirement explicitly changes it, and document intentional API or semantic changes.
