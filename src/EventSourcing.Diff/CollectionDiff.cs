using System.Collections.Immutable;

namespace EventSourcing.Diff;

/// <summary>Compares two values and returns the differences found at <paramref name="path"/>.</summary>
public delegate ImmutableArray<Difference> DiffComparison<in T>(T oldValue, T newValue, PropertyPath? path, DiffSettings? settings);

/// <summary>Collection operations used by generated comparisons. Collection traversal preserves enumeration order.</summary>
public static class CollectionDiff
{
    /// <summary>Compares dictionaries by key; removed and added entries are reported with <see cref="DifferenceKind.Removed"/> and <see cref="DifferenceKind.Added"/>.</summary>
    public static ImmutableArray<Difference> Dictionary<TKey, TValue>(
        IReadOnlyDictionary<TKey, TValue>? oldValue,
        IReadOnlyDictionary<TKey, TValue>? newValue,
        DiffComparison<TValue> compare,
        PropertyPath? path = null,
        DiffSettings? settings = null) where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(compare);
        var walk = Adapt(compare);
        return DiffCollector.Collect(oldValue, newValue, path, settings, (o, n, c) => c.Dictionary(o, n, walk));
    }

    /// <summary>Compares dictionaries by key, like the <see cref="IReadOnlyDictionary{TKey, TValue}"/> overload.</summary>
    public static ImmutableArray<Difference> Dictionary<TKey, TValue>(
        IDictionary<TKey, TValue>? oldValue,
        IDictionary<TKey, TValue>? newValue,
        DiffComparison<TValue> compare,
        PropertyPath? path = null,
        DiffSettings? settings = null) where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(compare);
        var walk = Adapt(compare);
        return DiffCollector.Collect(oldValue, newValue, path, settings, (o, n, c) => c.Dictionary(o, n, walk));
    }

    /// <summary>Compares sets by membership. Differences are reported at the set path; element comparisons are not used.</summary>
    public static ImmutableArray<Difference> Set<T>(
        IReadOnlySet<T>? oldValue,
        IReadOnlySet<T>? newValue,
        PropertyPath? path = null,
        DiffSettings? settings = null)
        => DiffCollector.Collect(oldValue, newValue, path, settings, static (o, n, c) => c.Set(o, n));

    /// <summary>Compares sets by membership. Differences are reported at the set path; element comparisons are not used.</summary>
    public static ImmutableArray<Difference> Set<T>(
        ISet<T>? oldValue,
        ISet<T>? newValue,
        PropertyPath? path = null,
        DiffSettings? settings = null)
        => DiffCollector.Collect(oldValue, newValue, path, settings, static (o, n, c) => c.Set(o, n));

    /// <summary>Compares collections matched by key, ignoring order. Throws <see cref="InvalidOperationException"/> for null elements, null keys or duplicate keys.</summary>
    public static ImmutableArray<Difference> Keyed<T, TKey>(
        IEnumerable<T>? oldValue,
        IEnumerable<T>? newValue,
        Func<T, TKey> key,
        DiffComparison<T> compare,
        PropertyPath? path = null,
        DiffSettings? settings = null) where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(compare);
        var walk = Adapt(compare);
        return DiffCollector.Collect(oldValue, newValue, path, settings, (o, n, c) => c.Keyed(o, n, key, walk));
    }

    /// <summary>Compares sequences by position; positions past the shorter sequence are reported as added or removed. With <paramref name="atomicElements"/>, the sequences are compared as one value.</summary>
    public static ImmutableArray<Difference> Enumerable<T>(
        IEnumerable<T>? oldValue,
        IEnumerable<T>? newValue,
        DiffComparison<T> compare,
        bool atomicElements,
        PropertyPath? path = null,
        DiffSettings? settings = null)
    {
        ArgumentNullException.ThrowIfNull(compare);
        var walk = Adapt(compare);
        return DiffCollector.Collect(oldValue, newValue, path, settings, (o, n, c) => c.Enumerable(o, n, walk, atomicElements));
    }

    static DiffWalk<T> Adapt<T>(DiffComparison<T> compare) => (o, n, c) => c.AddRange(compare(o, n, c.Path, c.Settings));
}