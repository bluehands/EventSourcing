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
        path ??= PropertyPath.Empty;
        if (settings?.PathsToIgnore.Contains(path) == true || ReferenceEquals(oldValue, newValue)) return [];
        if (oldValue is null || newValue is null) return [new(path, oldValue, newValue)];

        var result = ImmutableArray.CreateBuilder<Difference>();
        foreach (var pair in oldValue)
        {
            var itemPath = path.PushKey(pair.Key);
            if (settings?.PathsToIgnore.Contains(itemPath) == true) continue;
            if (newValue.TryGetValue(pair.Key, out var updated))
                result.AddRange(compare(pair.Value, updated, itemPath, settings));
            else
                result.Add(Difference.Removed(itemPath, pair.Value));
        }
        foreach (var pair in newValue)
        {
            var itemPath = path.PushKey(pair.Key);
            if (!oldValue.ContainsKey(pair.Key) && settings?.PathsToIgnore.Contains(itemPath) != true)
                result.Add(Difference.Added(itemPath, pair.Value));
        }
        return result.ToImmutable();
    }

    /// <summary>Compares dictionaries by key, like the <see cref="IReadOnlyDictionary{TKey, TValue}"/> overload.</summary>
    public static ImmutableArray<Difference> Dictionary<TKey, TValue>(
        IDictionary<TKey, TValue>? oldValue,
        IDictionary<TKey, TValue>? newValue,
        DiffComparison<TValue> compare,
        PropertyPath? path = null,
        DiffSettings? settings = null) where TKey : notnull
        => Dictionary(
            oldValue is null ? null : new ReadOnlyDictionaryAdapter<TKey, TValue>(oldValue),
            newValue is null ? null : new ReadOnlyDictionaryAdapter<TKey, TValue>(newValue),
            compare, path, settings);

    /// <summary>Compares sets by membership. Differences are reported at the set path; element comparisons are not used.</summary>
    public static ImmutableArray<Difference> Set<T>(
        IReadOnlySet<T>? oldValue,
        IReadOnlySet<T>? newValue,
        PropertyPath? path = null,
        DiffSettings? settings = null)
        => Set(oldValue, newValue, oldValue is null ? null : oldValue.Contains, newValue is null ? null : newValue.Contains,
            oldValue is null ? null : oldValue.SetEquals, path, settings);

    /// <summary>Compares sets by membership. Differences are reported at the set path; element comparisons are not used.</summary>
    public static ImmutableArray<Difference> Set<T>(
        ISet<T>? oldValue,
        ISet<T>? newValue,
        PropertyPath? path = null,
        DiffSettings? settings = null)
        => Set(oldValue, newValue, oldValue is null ? null : oldValue.Contains, newValue is null ? null : newValue.Contains,
            oldValue is null ? null : oldValue.SetEquals, path, settings);

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
        path ??= PropertyPath.Empty;
        if (settings?.PathsToIgnore.Contains(path) == true || ReferenceEquals(oldValue, newValue)) return [];
        if (oldValue is null || newValue is null) return [new(path, oldValue, newValue)];

        var oldItems = Index(oldValue, key, path);
        var newItems = Index(newValue, key, path);
        var result = ImmutableArray.CreateBuilder<Difference>();
        foreach (var (k, item) in oldItems)
        {
            var itemPath = path.PushKey(k);
            if (settings?.PathsToIgnore.Contains(itemPath) == true) continue;
            if (newItems.TryGetValue(k, out var updated)) result.AddRange(compare(item, updated, itemPath, settings));
            else result.Add(Difference.Removed(itemPath, item));
        }
        foreach (var (k, item) in newItems)
        {
            var itemPath = path.PushKey(k);
            if (!oldItems.ContainsKey(k) && settings?.PathsToIgnore.Contains(itemPath) != true)
                result.Add(Difference.Added(itemPath, item));
        }
        return result.ToImmutable();
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
        path ??= PropertyPath.Empty;
        if (settings?.PathsToIgnore.Contains(path) == true || ReferenceEquals(oldValue, newValue)) return [];
        if (oldValue is null || newValue is null) return [new(path, oldValue, newValue)];
        if (atomicElements)
            return oldValue.SequenceEqual(newValue) ? [] : [new(path, oldValue, newValue)];

        var result = ImmutableArray.CreateBuilder<Difference>();
        using var oldItems = oldValue.GetEnumerator();
        using var newItems = newValue.GetEnumerator();
        for (var index = 0; ; index++)
        {
            var hasOld = oldItems.MoveNext();
            var hasNew = newItems.MoveNext();
            if (!hasOld && !hasNew) break;
            var itemPath = path.PushIndex(index);
            if (settings?.PathsToIgnore.Contains(itemPath) == true) continue;
            if (!hasOld) result.Add(Difference.Added(itemPath, newItems.Current));
            else if (!hasNew) result.Add(Difference.Removed(itemPath, oldItems.Current));
            else result.AddRange(compare(oldItems.Current, newItems.Current, itemPath, settings));
        }
        return result.ToImmutable();
    }

    static ImmutableArray<Difference> Set<T>(
        IEnumerable<T>? oldValue,
        IEnumerable<T>? newValue,
        Func<T, bool>? oldContains,
        Func<T, bool>? newContains,
        Func<IEnumerable<T>, bool>? setEquals,
        PropertyPath? path,
        DiffSettings? settings)
    {
        path ??= PropertyPath.Empty;
        if (settings?.PathsToIgnore.Contains(path) == true || ReferenceEquals(oldValue, newValue)) return [];
        if (oldValue is null || newValue is null) return [new(path, oldValue, newValue)];
        if (setEquals!(newValue)) return [];

        var result = ImmutableArray.CreateBuilder<Difference>();
        foreach (var item in oldValue)
            if (!newContains!(item)) result.Add(Difference.Removed(path, item));
        foreach (var item in newValue)
            if (!oldContains!(item)) result.Add(Difference.Added(path, item));
        return result.ToImmutable();
    }

    static Dictionary<TKey, T> Index<T, TKey>(IEnumerable<T> items, Func<T, TKey> key, PropertyPath path) where TKey : notnull
    {
        var result = new Dictionary<TKey, T>();
        foreach (var item in items)
        {
            if (item is null) throw new InvalidOperationException($"Null element at '{path}'.");
            var k = key(item);
            if (k is null) throw new InvalidOperationException($"Null key at '{path}'.");
            if (!result.TryAdd(k, item)) throw new InvalidOperationException($"Duplicate key '{k}' at '{path}'.");
        }
        return result;
    }

    sealed class ReadOnlyDictionaryAdapter<TKey, TValue>(IDictionary<TKey, TValue> inner) : IReadOnlyDictionary<TKey, TValue> where TKey : notnull
    {
        public TValue this[TKey key] => inner[key];
        public IEnumerable<TKey> Keys => inner.Keys;
        public IEnumerable<TValue> Values => inner.Values;
        public int Count => inner.Count;
        public bool ContainsKey(TKey key) => inner.ContainsKey(key);
        public bool TryGetValue(TKey key, out TValue value) => inner.TryGetValue(key, out value!);
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => inner.GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}

