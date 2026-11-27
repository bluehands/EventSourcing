# EventSourcing.Diff.Benchmarks

BenchmarkDotNet micro-benchmarks for the generated `EventSourcing.Diff` comparisons (time and allocated bytes per call). Used to measure the steps of `ai-plans/diff-runtime-performance.md`. Separate from the event-store workload runner in `EventSourcing.Benchmarks`.

## Run

```powershell
dotnet run -c Release --project src/EventSourcing.Diff.Benchmarks -- --filter * --job short
```

Omit `--job short` for a more precise (much slower) run. Output goes to `BenchmarkDotNet.Artifacts/` (git-ignored).

## Model and scenarios

`Model.cs` defines a replay-like `State` with 19 properties and 3 levels of nesting (`State` > `Profile` > `Address` > `Geo`): `int`, `string`, `Guid`, `DateTimeOffset`, enum, `decimal`, `bool`, a nested record, a polymorphic `Payload` (abstract record with two derived records), `ImmutableArray<Item>` (20), `ImmutableDictionary<string, Entry>` (20) and a `MatchBy`-keyed `ImmutableArray<Line>` (50). Data is deterministic.

`Scenarios.cs` builds the compared pairs; `DiffBenchmarks` runs each one with `WithIgnoreList` false and true (ignores the root `Counter` property, which is never on a changed route).

| Scenario | Description |
|---|---|
| SameReference | `Diff(a, a)` |
| UnchangedSharedCopy | `a` vs `a with { }` (shared substructure, nothing changed) |
| EqualSeparateInstances | two separately built, equal states |
| OneDeepChange | one leaf changed at `Profile.Address.Geo.Region` |
| OneKeyedElementChange | one `Line` property changed (`Lines[1025].Quantity`) |
| ManyDictionaryChanges | 5 entries changed, 3 removed, 2 added |

Each scenario also has a `HasDifferences...` benchmark with the same data and settings.

## Results

### Baseline (before optimization)

2026-11-27, `--job short`. BenchmarkDotNet 0.15.8, Windows 11 (24H2), 13th Gen Intel Core i9-13950HX, .NET SDK 10.0.403, .NET 10.0.14 X64 RyuJIT x86-64-v3.

| Scenario | WithIgnoreList | Mean | Allocated |
|---|---|--:|--:|
| SameReference | False | 3.8 ns | 24 B |
| UnchangedSharedCopy | False | 5,695 ns | 24,336 B |
| EqualSeparateInstances | False | 11,453 ns | 73,512 B |
| OneDeepChange | False | 6,146 ns | 25,952 B |
| OneKeyedElementChange | False | 5,939 ns | 25,064 B |
| ManyDictionaryChanges | False | 9,181 ns | 31,784 B |
| SameReference | True | 5.9 ns | 24 B |
| UnchangedSharedCopy | True | 10,204 ns | 24,336 B |
| EqualSeparateInstances | True | 40,637 ns | 73,512 B |
| OneDeepChange | True | 11,458 ns | 25,952 B |
| OneKeyedElementChange | True | 10,539 ns | 25,064 B |
| ManyDictionaryChanges | True | 15,639 ns | 31,784 B |

Short-job errors are large (up to ±50%); compare orders of magnitude, not small deltas.

### After optimization (2026-11-27)

Same machine and `--job short`. The 24 B in `Diff` rows for unchanged inputs is boxing of the returned `ImmutableArray` to `object` in the benchmark itself.

`Diff`:

| Scenario | WithIgnoreList | Mean | Allocated |
|---|---|--:|--:|
| SameReference | False | 33.7 ns | 24 B |
| UnchangedSharedCopy | False | 69.2 ns | 24 B |
| EqualSeparateInstances | False | 5,409 ns | 10,584 B |
| OneDeepChange | False | 199.6 ns | 416 B |
| OneKeyedElementChange | False | 3,050 ns | 10,968 B |
| ManyDictionaryChanges | False | 1,865 ns | 2,072 B |
| SameReference | True | 39.3 ns | 24 B |
| UnchangedSharedCopy | True | 75.8 ns | 24 B |
| EqualSeparateInstances | True | 5,612 ns | 10,584 B |
| OneDeepChange | True | 209.8 ns | 416 B |
| OneKeyedElementChange | True | 2,989 ns | 10,968 B |
| ManyDictionaryChanges | True | 1,915 ns | 2,072 B |

`HasDifferences`:

| Scenario | WithIgnoreList | Mean | Allocated |
|---|---|--:|--:|
| SameReference | False | 32.2 ns | 0 B |
| UnchangedSharedCopy | False | 61.0 ns | 0 B |
| EqualSeparateInstances | False | 5,557 ns | 10,560 B |
| OneDeepChange | False | 95.3 ns | 0 B |
| OneKeyedElementChange | False | 2,329 ns | 9,984 B |
| ManyDictionaryChanges | False | 70.7 ns | 0 B |
| SameReference | True | 21.6 ns | 0 B |
| UnchangedSharedCopy | True | 71.1 ns | 0 B |
| EqualSeparateInstances | True | 5,437 ns | 10,560 B |
| OneDeepChange | True | 109.8 ns | 0 B |
| OneKeyedElementChange | True | 1,267 ns | 9,984 B |
| ManyDictionaryChanges | True | 139.6 ns | 0 B |

Comparison with the baseline for `Diff` (no ignore list): UnchangedSharedCopy is about 80x faster and 24 KB to 24 B (the remaining bytes are the benchmark's boxing); EqualSeparateInstances about 2x faster with 7x fewer bytes; OneDeepChange about 30x faster with 62x fewer bytes; OneKeyedElementChange about 2x faster with 2.3x fewer bytes; ManyDictionaryChanges about 5x faster with 15x fewer bytes. With an ignore list the gains are larger in time (e.g. EqualSeparateInstances 40.6 µs to 5.6 µs). `HasDifferences` allocates nothing except for keyed lists and is another 2x or more faster when a difference exists (OneDeepChange, ManyDictionaryChanges).

Remaining costs: keyed lists that are not element-identical build key lookups (about 10 KB for the 50-element list), which dominates EqualSeparateInstances and OneKeyedElementChange. Recorded differences allocate their path, boxed values and the result list.
