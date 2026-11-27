# Bluehands.EventSourcing.Diff

Source-generated structural comparisons for immutable .NET models. The package contains its runtime and source generator; it does not depend on the event store, command layer, or a persistence provider.

Requires .NET 10. Generated code requires C# 12 or later. The generator targets .NET Standard 2.0 and references Roslyn 4.11.

## Usage

Reference `Bluehands.EventSourcing.Diff`, then declare a top-level, non-generic static partial class:

```csharp
using EventSourcing.Diff;

public record Address(string City);
public record Customer(string Name, Address Address, int Version);

[Diff(typeof(Customer), nameof(Customer.Version))]
public static partial class CustomerComparisons;
```

Call a generated `Diff` overload, or the extension methods for types named in `[Diff]`:

```csharp
var before = new Customer("Alice", new("Paris"), 1);
var after = new Customer("Alice", new("Berlin"), 2);

var differences = CustomerComparisons.Diff(before, after);
// One difference: Address.City, old value "Paris", new value "Berlin".

var same = before.Diff(after);              // extension, same result
bool changed = before.HasDifferences(after); // true
```

Results are `ImmutableArray<Difference>`. Each difference contains `PropertyPath`, `OldValue`, and `NewValue`. Nested model and collection overloads are generated automatically. No `partial` modifier or attributes are required on the models themselves.

## HasDifferences

`HasDifferences(oldValue, newValue, settings)` returns whether any difference exists and stops at the first one, without building a result list. The contract is `HasDifferences(a, b, s) == !Diff(a, b, null, s).IsEmpty`. The one exception: because it stops early, it may not reach invalid keyed data (duplicate or null keys) further on, and identical keyed lists skip key validation (see below).

`Diff` and `HasDifferences` are generated as extension methods (`old.Diff(new)`, `old.HasDifferences(new)`) only for types named in a `[Diff(typeof(T))]` attribute. Overloads generated for types reached only through properties (e.g. `string`, collections) are plain static methods, so they don't appear on common types. `CustomerComparisons.Diff(a, b)` keeps working. `DiffCollector` and `DiffWalk` are public only because generated code uses them; don't call them directly.

## Performance

- Unchanged parts that are the same reference are skipped, so replay states that reuse unchanged substructure are cheap.
- Paths are only built for recorded differences (and custom comparisons).
- Ignore lists are checked without building paths.
- Comparing unchanged states allocates nothing, except keyed lists whose elements are not all reference-identical (they build key lookups).

Measurements are in [the benchmark README](../EventSourcing.Diff.Benchmarks/README.md).

## Comparison contract

- **Objects:** readable public instance properties, including inherited properties, in ordinal property-name order. Indexers, static properties, and inaccessible getters are excluded. Shared references are skipped, so models must not be mutated in place.
- **Atomic values:** non-collection value types, strings, `object`, delegates, and framework types (`System.*`/`Microsoft.*` from referenced assemblies that are not `IEnumerable<T>`, e.g. `Uri`, `Version`, `Type`, `Encoding`, `CultureInfo`, `Exception`) use `EqualityComparer<T>.Default`. Structs are not traversed. A property typed as `object` is compared by equality, not by inspecting its runtime model.
- **Null or runtime-type changes:** one whole-object difference at the current path. A null collection is distinct from an empty collection.
- **Dictionaries:** types implementing `IReadOnlyDictionary<TKey, TValue>` or `IDictionary<TKey, TValue>` are matched by key. Removed and modified entries follow old enumeration order; additions follow new enumeration order. Key equality/comparers must be consistent between both dictionaries.
- **Sets:** types implementing `IReadOnlySet<T>` or `ISet<T>` are compared by membership (`SetEquals`), ignoring order. Differences are reported at the set path as `Removed` (old elements missing from the new set) then `Added`; elements are not traversed.
- **Enumerables:** types implementing `IEnumerable<T>` are ordered. Atomic-element collections report a whole-collection difference; complex elements are compared by index, with positions past the shorter list reported as `Added`/`Removed`. Insertions/removals can therefore produce positional differences (use key-based matching to avoid this). Enumerables must be finite and safe to enumerate.
- **Nullable structs and immutable arrays:** `T?` is compared like `T` when both have values (e.g. `ImmutableArray<int>?`); a null transition is one `Changed`. Default and empty immutable arrays are distinct; a default/non-default transition is one whole-array difference.
- **Polymorphism:** accessible, closed, concrete subclasses and interface implementations declared in the consumer assembly are discovered automatically, most-specific first. Concrete base instances are compared normally. Two distinct instances of an unknown runtime subtype throw `NotSupportedException` rather than silently losing changes. Use `TreatAsValue` or a custom comparison for external/open hierarchies.
- **Graphs:** acyclic immutable models only. Recursive model *definitions* are supported, but comparing distinct cyclic object graphs is not. Getters and custom comparisons can throw; exceptions propagate.

Results hold references to the original values, not serialized snapshots; the result is not an executable patch format. Values can contain secrets: filter or format them before logging.

`Difference.Kind` is `Added` or `Removed` for membership: dictionary entries, list positions past the shorter list, set elements, and keyed elements (`OldValue` or `NewValue` is `null` respectively, so null-valued entries stay distinguishable). Everything else is `Changed`, including null transitions, runtime-type changes, and whole-collection differences.

## Ignore properties and paths

`[Diff(typeof(T), nameof(T.Property))]` excludes a property at generation time. Declarations on base types and interfaces also apply to subclasses/implementations within the same comparison class. Multiple declarations for the same type combine their ignored properties.

For runtime exclusions, build typed paths:

```csharp
var settings = new DiffSettings([
    PropertyPath.Empty.Push("Address"),
    PropertyPath.Empty.Push("Customers").PushKey("internal")
]);
var differences = CustomerComparisons.Diff(before, after, settings: settings);
```

Comparison stops at an exactly ignored path, including its descendants. This applies to dictionary and complex-collection additions/removals too. Ignore an atomic collection at its collection path, not by index. Ignoring `PropertyPath.Empty` excludes the whole root.

Paths have structural equality and immutable `Segments` in root-to-leaf order. `Push`, `PushKey`, and `PushIndex` distinguish properties, dictionary keys, and collection indices. Keys must have stable equality and hash codes. `ToString()` returns a display path such as `Customers[internal].Address.City`; it is not an escaped, round-trippable serialization format.

## Treat a domain type as atomic

```csharp
public interface IEntityId { long Value { get; } }
public record CustomerId(long Value) : IEntityId;

[Diff(typeof(CustomerId))]
[Diff(typeof(IEntityId), TreatAsValue = true)]
public static partial class IdComparisons;
```

`TreatAsValue` applies to the selected type and its subclasses/implementations. This keeps domain IDs at one path without any application-specific rules in the generator.

## Key-based matching

Match complex collection elements by an explicit key instead of position:

```csharp
[Diff(typeof(Order))]
[Diff(typeof(OrderLine), MatchBy = nameof(OrderLine.Id))]
public static partial class OrderComparisons;
```

- `MatchBy` names a readable public instance property (not a collection) and is configured per element type. It applies to subclasses and implementations, so a key on a base interface works for polymorphic lists. Different `MatchBy` values for one type in a comparison class are an error.
- Applies to lists, arrays, immutable arrays, and sets of that element type, and takes precedence over set membership. Order is ignored.
- Matched elements are compared recursively with paths like `Lines[42].Quantity`; unmatched ones are `Removed`/`Added` at `Lines[42]`. Ignored paths apply to keyed entries too.
- Duplicate keys, null keys, and null elements throw `InvalidOperationException` naming the collection path. Validation only runs when the lists are not element-for-element reference-identical; identical lists return early without validation.
## Custom comparisons

Supply a static `Diff` overload in the comparison class (any partial declaration). Its first two parameter types must match, followed by `PropertyPath?` and `DiffSettings?`, returning `ImmutableArray<Difference>`:

```csharp
using System;
using System.Collections.Immutable;

public static partial class CustomerComparisons
{
    public static ImmutableArray<Difference> Diff(
        string? oldValue, string? newValue,
        PropertyPath? path = null, DiffSettings? settings = null)
    {
        path ??= PropertyPath.Empty;
        if (settings?.PathsToIgnore.Contains(path) == true) return [];
        return StringComparer.OrdinalIgnoreCase.Equals(oldValue, newValue)
            ? []
            : [new(path, oldValue, newValue)];
    }
}
```

Custom methods own null handling, exclusions below their own path, and their returned differences; generated code doesn't call them at an exactly ignored path. Build results with `Difference.Changed(path, old, new)`, `Difference.Added(path, value)`, and `Difference.Removed(path, value)`; the `Difference` constructor defaults `Kind` to `Changed`. Collections with custom element comparisons are compared element-by-element, rather than bypassing those methods with `SequenceEqual`.

## Diagnostics and limits

| Diagnostic | Meaning |
|---|---|
| `ESDIFF001` | Comparison class must be a non-file-local, top-level, non-generic static partial class |
| `ESDIFF002` | Unsupported model (reported at the `[Diff]` attribute with the property path): inaccessible/file-local/open type, ref struct, multidimensional array, pointer, or dynamic |
| `ESDIFF003` | Invalid custom comparison signature |
| `ESDIFF004` | Ambiguous property inherited from several interfaces; ignore it or provide a custom comparison |
| `ESDIFF005` | Warning: an ignored property name matches no property of the type |
| `ESDIFF006` | Invalid `MatchBy` property (missing, not readable/public/instance, indexer, or a collection) |
| `ESDIFF007` | Conflicting `MatchBy` values for the same type |

Limitations:

- Dictionary lookups use each dictionary's own comparer; mismatched comparers can give asymmetric results.
- Structs (including record structs) are not traversed; they are compared by equality.
- Unknown runtime subtypes from other assemblies throw `NotSupportedException`.
- Set elements are not traversed; keyed collections ignore order.
- Glob patterns, cycle detection, patch generation, and automatic serialization/redaction are not included.
## Development checks

```powershell
dotnet test --project src/EventSourcing.Diff.Test/EventSourcing.Diff.Test.csproj --configuration Release
dotnet pack src/EventSourcing.Diff/EventSourcing.Diff.csproj --configuration Release --output artifacts/diff-package
./src/EventSourcing.Diff.Test/Test-Package.ps1 -PackageDirectory ./artifacts/diff-package -WorkingDirectory "$env:TEMP/diff-package-smoke"
```

The package smoke check uses an isolated NuGet cache and two consumer assemblies, without project references to this repository. It verifies analyzer loading, shared runtime type identity, nested comparisons of referenced models, packaged XML documentation, framework types, sets, keyed matching, `HasDifferences`, extension methods, and rejection of unknown external runtime subclasses.
