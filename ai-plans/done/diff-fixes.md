# Diff pre-release fixes and key-based matching

Status: Steps 1–7 done. Scope: `src/EventSourcing.Diff`, `src/EventSourcing.Diff.SourceGenerator`, `src/EventSourcing.Diff.Test`.
Nothing is published yet, so public API changes are allowed. Versioning and `AnalyzerReleases.Shipped.md` are out of scope.

Generator performance is planned separately in `../open/diff-generator-performance.md`.

## Findings that motivate this plan

Reproduced with a scratch consumer project:

- A property typed `Encoding` fails generation (`ESDIFF002 … ReadOnlySpan<byte>`) and disables the whole comparison class, without naming the property.
- A property typed `Exception` generates ~1,000 lines walking reflection types and emits `SYSLIB0012/0005/0050` obsolete warnings (breaks `TreatWarningsAsErrors`).
- Equal `CultureInfo`/`Exception` values throw `NotSupportedException` at runtime (unknown framework subtypes such as `GregorianCalendar`).
- `HashSet<string>{a,b}` vs `{b,a}` reports a difference.
- An `IDictionary<K,V>`-typed property is compared as an ordered sequence of pairs; equal dictionaries with different insertion order differ.
- `ImmutableArray<int>?` is compared with struct equality (array reference), so equal contents differ.
- `interface IC : IA, IB` with a conflicting property `X` produces compiler error `CS0229` in generated code.
- Removing a dictionary entry whose value is null yields `null -> null`; indistinguishable from other changes.
- A misspelled ignored property name is silently accepted.
- Generator tests only fail on errors, not on warnings in generated code.

## Decisions

- Framework types (`System.*` / `Microsoft.*`, from referenced assemblies, not implementing `IEnumerable<T>`) are compared by equality.
- Sets: `SetEquals`; if not equal, report `Removed`/`Added` per element.
- Conflicting interface properties: clear generator error.
- `DifferenceKind` is added now.
- Key-based matching is included, configured per element type with `MatchBy`. Duplicate/null keys and null elements throw. Keyed collections ignore order.

## Step 1: DifferenceKind (runtime) (done)

Done: `Difference.cs` contains

```csharp
public enum DifferenceKind { Changed, Added, Removed }
public sealed record Difference(PropertyPath PropertyPath, object? OldValue, object? NewValue, DifferenceKind Kind = DifferenceKind.Changed)
{
    public static Difference Changed(PropertyPath path, object? oldValue, object? newValue);
    public static Difference Added(PropertyPath path, object? value);   // OldValue null
    public static Difference Removed(PropertyPath path, object? value); // NewValue null
}
```

Semantics:

- `Added` / `Removed`: membership of dictionary entries, list positions beyond the shorter list, set elements, keyed elements.
- `Changed`: everything else, including null transitions, runtime-type changes and whole-collection differences.

Remaining: use the factories in `CollectionDiff` (dictionary additions/removals, enumerable positions past the end).

## Step 2: Framework types compared by equality (generator) (done)

In `DiffGenerator.IsAtomic`, replace the hardcoded `Uri`/`Version`/`Type` check with: the type is not declared in source of the current compilation (`type.Locations.All(l => l.IsInMetadata)` or `!SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, compilation.Assembly)`), its top-level namespace is `System` or `Microsoft`, and it does not implement `IEnumerable<T>` (so framework collections are still treated as collections). Keep `string`, `object`, delegates and non-enumerable value types atomic.

Tests: models with `Encoding`, `Exception`, `CultureInfo`, `Regex` properties generate without warnings or errors; equal values produce nothing; different values produce one `Changed` at the property; generated source does not contain `System.Reflection`.

## Step 3: Collections (done)

Generator detection order in `Visit`: atomic/custom → nullable struct → dictionary (`IReadOnlyDictionary<,>`, then `IDictionary<,>`) → keyed element (step 4) → set (`IReadOnlySet<T>`, then `ISet<T>`) → `IEnumerable<T>` → object.

Generated calls must cast explicitly to the detected interface (e.g. `(global::System.Collections.Generic.IReadOnlySet<T>?)oldValue`) because types such as `HashSet<T>` and `Dictionary<K,V>` implement both interfaces and overloads would otherwise be ambiguous. `ImmutableArray<T>` keeps its `IsDefault` guard on every path that enumerates it.

Runtime additions in `CollectionDiff`:

```csharp
public static ImmutableArray<Difference> Dictionary<TKey, TValue>(IDictionary<TKey, TValue>? oldValue, IDictionary<TKey, TValue>? newValue,
    DiffComparison<TValue> compare, PropertyPath? path = null, DiffSettings? settings = null) where TKey : notnull;
public static ImmutableArray<Difference> Set<T>(IReadOnlySet<T>? oldValue, IReadOnlySet<T>? newValue, PropertyPath? path = null, DiffSettings? settings = null);
public static ImmutableArray<Difference> Set<T>(ISet<T>? oldValue, ISet<T>? newValue, PropertyPath? path = null, DiffSettings? settings = null);
```

- `IDictionary` overload: wrap in an internal read-only adapter and reuse the existing algorithm.
- `Set`: ignored path → `[]`; same reference → `[]`; either null → one `Changed`; `SetEquals` → `[]`; otherwise `Removed(path, element)` for old elements not contained in the new set (old enumeration order), then `Added(path, element)` for new elements not contained in the old set (new enumeration order). Each set's own `Contains` is used. All set differences use the set's path; custom element comparisons are not used for sets (document this).

Nullable structs: if `T?` has a non-atomic underlying `T` (e.g. `ImmutableArray<int>?`, a struct dictionary/enumerable), `IsAtomic` treats `T?` like `T`, and the generated method is:

```csharp
if (!oldValue.HasValue || !newValue.HasValue) return oldValue.HasValue == newValue.HasValue ? [] : [new(path, oldValue, newValue)];
return Diff(oldValue.Value, newValue.Value, path, settings);
```

Tests: reordered `HashSet<string>` / `ImmutableHashSet<string>` → nothing; set add/remove kinds and values; reordered `IDictionary`-typed property → nothing; per-key changes on `IDictionary`; `ImmutableArray<int>?` equal contents → nothing, null transitions → one `Changed`; runtime tests for `Set` and `IDictionary` overloads.

## Step 4: Key-based matching (`MatchBy`) (done)

Done: `DiffAttribute.MatchBy` (`string?`).

```csharp
[Diff(typeof(Order))]
[Diff(typeof(OrderLine), MatchBy = nameof(OrderLine.Id))]
public static partial class OrderComparisons;
```

Runtime:

```csharp
public static ImmutableArray<Difference> Keyed<T, TKey>(IEnumerable<T>? oldValue, IEnumerable<T>? newValue, Func<T, TKey> key,
    DiffComparison<T> compare, PropertyPath? path = null, DiffSettings? settings = null) where TKey : notnull;
```

- Ignored path → `[]`; same reference → `[]`; either null → one `Changed`.
- Build a key lookup of each side (`EqualityComparer<TKey>.Default`). Throw `InvalidOperationException` for a duplicate key, a null element or a null key; the message names the collection path and the key, e.g. `Duplicate key '42' at 'Order.Lines'.`
- Walk old elements in order: missing in new → `Removed(path.PushKey(key), element)`; present → `compare(old, new, path.PushKey(key), settings)`. Then walk new elements in order: missing in old → `Added(path.PushKey(key), element)`. Skip any item path contained in `PathsToIgnore`.
- Reordering produces no differences.

Generator:

- `Configuration.MatchBy` is read from the named argument. Resolution follows `TreatAsValue`: a key declared on a type applies to its subclasses and implementations (search `RelatedTypes(elementType)`).
- Keyed matching applies to sets and enumerables whose element type has a key, and takes precedence over set membership and whole-collection comparison of atomic elements. Dictionaries are unchanged.
- Emit `CollectionDiff.Keyed<TElement, TKey>(oldValue, newValue, static x => x.@Key, Diff, path, settings)`. Matched pairs use the generated or custom element `Diff` (with `TreatAsValue`, a changed matched element is one `Changed` at `[key]`).
- `ESDIFF006` (error): the `MatchBy` property does not exist on the configured type hierarchy, is not a readable public instance non-indexer property, or its type is a collection (implements `IEnumerable<T>` and is not `string`). Report at the attribute.
- `ESDIFF007` (error): two `[Diff]` attributes in the same comparison class declare different `MatchBy` values for the same type.

Tests: reorder → nothing; insertion affects only its key (`Added` at `Lines[3]`); change inside a matched element uses the keyed path (`Lines[42].Quantity`); duplicate key / null element / null key throw with the path in the message; ignore paths for added, removed and modified keyed entries; key declared on a base interface used for a polymorphic list; custom element comparison used for matched pairs; keyed `ImmutableArray` (including default) and keyed `HashSet`; `ESDIFF006` and `ESDIFF007` fire.

## Step 5: Conflicting interface properties (done)

`ESDIFF004` (error): `'{type}' has ambiguous property '{name}' inherited from '{A}' and '{B}'; ignore it or provide a custom comparison.`
For interface models, group candidate properties by name; ambiguous when no single property's containing type derives from (implements) all other containing types in the group. A `new` redeclaration in the derived interface is not ambiguous. Ignored properties are excluded before the check, so ignoring the property resolves the error.

Test: `IC : IA, IB` with conflicting `X` → `ESDIFF004`, no `CS0229`; ignoring `X` compiles.

## Step 6: Polish (done)

- `ESDIFF002`: track the property path through which each type was first reached and report at the `[Diff]` attribute that introduced the root type (`AttributeData.ApplicationSyntaxReference`). Message: `Cannot generate a comparison for '{0}' (reached via '{1}'): {2}`. Replace "provide an atomic declaration" with "use [Diff(typeof(...), TreatAsValue = true)] or a custom comparison".
- Generated `NotSupportedException` message includes the runtime type and the path: `$"Runtime type '{oldValue.GetType()}' at '{path}' is not known to the diff generator. ..."`.
- `ESDIFF005` (warning): an ignored property name matches no public instance property of the configured type, its base types or interfaces. Report at the attribute.
- `GeneratorTests.Generate` also fails on warnings located in generated syntax trees.
- `EventSourcing.Diff.csproj`: `<GenerateDocumentationFile>true</GenerateDocumentationFile>`; XML docs on every public type and member (`PropertyPath`, `PathSegment` and its records, `CollectionDiff`, `DiffComparison`). The build must have no `CS1591` warnings.
- Hint names: `{host full name}.{8-hex stable FNV-1a hash of the full name}.Diff.g.cs`. Do not use `string.GetHashCode()` (randomized per process). The hash keeps case-only variants apart, because Roslyn compares hint names case-insensitively.
- Namespace emission escapes keyword parts (`namespace @event;`): use a `SymbolDisplayFormat` with `globalNamespaceStyle: Omitted`, `typeQualificationStyle: NameAndContainingTypesAndNamespaces`, `miscellaneousOptions: EscapeKeywordIdentifiers`.
- Add `ESDIFF004`–`ESDIFF007` to `AnalyzerReleases.Unshipped.md`.

Tests: `ESDIFF002` message contains the via-path; `ESDIFF005` warning for `"Nmae"`; comparison class in `namespace @event`; hint names for `A.Comparisons` and `A.comparisons` are distinct and readable.

## Step 7: Package and documentation (done)

- `Test-Package.ps1`: assert `lib/net10.0/EventSourcing.Diff.xml` is packed; the consumer (TreatWarningsAsErrors) adds a model with `Exception`, `HashSet<string>` and a `MatchBy` keyed list, and checks `Kind` values.
- `src/EventSourcing.Diff/README.md`: update comparison contract (framework types, sets, `IDictionary`, nullable structs, keyed matching, `Kind`), diagnostics table (`ESDIFF001`–`ESDIFF007`), remove the "missing entry vs null value is ambiguous" caveat, document remaining limitations (dictionary comparer asymmetry, record structs not traversed, subtypes from other assemblies throw, set elements are not traversed, keyed collections ignore order).
- `ai-plans/diff-improvements.md` (now split into `done/` and `open/`): mark items 2, 3 and the implemented parts of 4 as done; note sets are handled via `SetEquals`.

## Verification (after every step)

```powershell
dotnet test --project src/EventSourcing.Diff.Test/EventSourcing.Diff.Test.csproj --configuration Release
dotnet build src/EventSourcing.slnx --configuration Release
dotnet pack src/EventSourcing.Diff/EventSourcing.Diff.csproj --configuration Release --output $env:TEMP/opencode/diff-package
./src/EventSourcing.Diff.Test/Test-Package.ps1 -PackageDirectory $env:TEMP/opencode/diff-package -WorkingDirectory $env:TEMP/opencode/diff-package-smoke
```
