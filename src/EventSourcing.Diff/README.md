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

Call a generated `Diff` overload:

```csharp
var before = new Customer("Alice", new("Paris"), 1);
var after = new Customer("Alice", new("Berlin"), 2);

var differences = CustomerComparisons.Diff(before, after);
// One difference: Address.City, old value "Paris", new value "Berlin".
```

Results are `ImmutableArray<Difference>`. Each difference contains `PropertyPath`, `OldValue`, and `NewValue`. Nested model and collection overloads are generated automatically. No `partial` modifier or attributes are required on the models themselves.

## Comparison contract

- **Objects:** readable public instance properties, including inherited properties, in ordinal property-name order. Indexers, static properties, and inaccessible getters are excluded. Shared references are skipped, so models must not be mutated in place.
- **Atomic values:** non-collection value types (including nullable values), strings, `object`, delegates, `Uri`, `Version`, and `Type` use `EqualityComparer<T>.Default`. Structs are not traversed. A property typed as `object` is compared by equality, not by inspecting its runtime model.
- **Null or runtime-type changes:** one whole-object difference at the current path. A null collection is distinct from an empty collection.
- **Dictionaries:** types implementing `IReadOnlyDictionary<TKey, TValue>` are matched by key. Removed and modified entries follow old enumeration order; additions follow new enumeration order. Key equality/comparers must be consistent between both dictionaries. The `IDictionary<TKey, TValue>` interface alone is not recognized as a keyed dictionary.
- **Enumerables:** types implementing `IEnumerable<T>` are ordered. Atomic-element collections report a whole-collection difference; complex elements are compared by index. Insertions/removals can therefore produce positional differences. Sets are also compared in enumeration order. Enumerables must be finite and safe to enumerate.
- **Immutable arrays:** default and empty arrays are distinct; a default/non-default transition is one whole-array difference.
- **Polymorphism:** accessible, closed, concrete subclasses and interface implementations declared in the consumer assembly are discovered automatically, most-specific first. Concrete base instances are compared normally. Two distinct instances of an unknown runtime subtype throw `NotSupportedException` rather than silently losing changes. Use an explicit atomic declaration or a custom comparison for external/open hierarchies.
- **Graphs:** acyclic immutable models only. Recursive model *definitions* are supported, but comparing distinct cyclic object graphs is not. Getters and custom comparisons can throw; exceptions propagate.

Results hold references to the original values, not serialized snapshots. A missing dictionary/list entry and an entry whose value is null can produce the same old/new values; the result is not an executable patch format. Values can contain secrets: filter or format them before logging.

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

Custom methods own null handling, path exclusions, and their returned differences. Collections with custom element comparisons are compared element-by-element, rather than bypassing those methods with `SequenceEqual`.

## Diagnostics and limits

| Diagnostic | Meaning |
|---|---|
| `ESDIFF001` | Comparison class must be a non-file-local, top-level, non-generic static partial class |
| `ESDIFF002` | Unsupported model: inaccessible/file-local/open type, ref struct, multidimensional array, pointer, or dynamic |
| `ESDIFF003` | Invalid custom comparison signature |

Unordered/identity-based matching, glob patterns, cycle detection, patch generation, and automatic serialization/redaction are intentionally not included.

## Development checks

```powershell
dotnet test --project src/EventSourcing.Diff.Test/EventSourcing.Diff.Test.csproj --configuration Release
dotnet pack src/EventSourcing.Diff/EventSourcing.Diff.csproj --configuration Release --output artifacts/diff-package
./src/EventSourcing.Diff.Test/Test-Package.ps1 -PackageDirectory ./artifacts/diff-package -WorkingDirectory "$env:TEMP/diff-package-smoke"
```

The package smoke check uses an isolated NuGet cache and two consumer assemblies, without project references to this repository. It verifies analyzer loading, shared runtime type identity, nested comparisons of referenced models, and rejection of unknown external runtime subclasses.
