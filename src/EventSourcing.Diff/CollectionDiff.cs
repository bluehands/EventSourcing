using System.Collections.Immutable;

namespace EventSourcing.Diff;

public delegate ImmutableArray<Difference> DiffComparison<in T>(T oldValue, T newValue, PropertyPath? path, DiffSettings? settings);

/// <summary>Collection operations used by generated comparisons. Collection traversal preserves enumeration order.</summary>
public static class CollectionDiff
{
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
                result.Add(new(itemPath, pair.Value, null));
        }
        foreach (var pair in newValue)
        {
            var itemPath = path.PushKey(pair.Key);
            if (!oldValue.ContainsKey(pair.Key) && settings?.PathsToIgnore.Contains(itemPath) != true)
                result.Add(new(itemPath, null, pair.Value));
        }
        return result.ToImmutable();
    }

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
            if (!hasOld) result.Add(new(itemPath, null, newItems.Current));
            else if (!hasNew) result.Add(new(itemPath, oldItems.Current, null));
            else result.AddRange(compare(oldItems.Current, newItems.Current, itemPath, settings));
        }
        return result.ToImmutable();
    }
}
