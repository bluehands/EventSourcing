using System.Collections.Immutable;
using System.ComponentModel;

namespace EventSourcing.Diff;

/// <summary>Compares two values and records differences into <paramref name="collector"/> at its current path. For generated code.</summary>
public delegate void DiffWalk<in T>(T oldValue, T newValue, DiffCollector collector);

/// <summary>
/// Collects differences while generated code walks two models. For generated code only.
/// Tracks the current location as a segment stack, so paths are materialized only for recorded differences and custom comparisons.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class DiffCollector
{
    [ThreadStatic] static DiffCollector? cached;

    DiffSettings? _settings;
    PropertyPath _start = PropertyPath.Empty;
    bool _stopAtFirst;
    List<Difference>? _list;
    int _depth;
    byte[] _kinds = new byte[16];
    object?[] _values = new object?[16];
    int[] _indices = new int[16];
    IgnoreNode?[] _nodes = new IgnoreNode?[16];
    PropertyPath?[] _paths = new PropertyPath?[16];

    DiffCollector() { }

    /// <summary>The settings of the root call.</summary>
    public DiffSettings? Settings => _settings;

    /// <summary>True in stop-at-first mode once a difference was found. Walks should return when it is set.</summary>
    public bool Stopped { get; private set; }

    /// <summary>The current path: the start path plus the entered segments. Cached per stack level.</summary>
    public PropertyPath Path => Materialize(_depth);

    /// <summary>Collects all differences between two values. Runs <paramref name="walk"/> with a rented collector.</summary>
    public static ImmutableArray<Difference> Collect<T>(T oldValue, T newValue, PropertyPath? path, DiffSettings? settings, DiffWalk<T> walk)
    {
        ArgumentNullException.ThrowIfNull(walk);
        var collector = Rent();
        try
        {
            if (!collector.Init(path, settings, false)) return [];
            walk(oldValue, newValue, collector);
            return collector._list is null ? [] : [.. collector._list];
        }
        finally { Return(collector); }
    }

    /// <summary>Returns whether two values differ, stopping at the first difference. Keyed data after it is not validated.</summary>
    public static bool HasDifferences<T>(T oldValue, T newValue, PropertyPath? path, DiffSettings? settings, DiffWalk<T> walk)
    {
        ArgumentNullException.ThrowIfNull(walk);
        var collector = Rent();
        try
        {
            if (!collector.Init(path, settings, true)) return false;
            walk(oldValue, newValue, collector);
            return collector.Stopped;
        }
        finally { Return(collector); }
    }

    static DiffCollector Rent()
    {
        var collector = cached;
        if (collector is null) return new();
        cached = null;
        return collector;
    }

    static void Return(DiffCollector collector)
    {
        collector.Clear();
        cached = collector;
    }

    bool Init(PropertyPath? path, DiffSettings? settings, bool stopAtFirst)
    {
        _settings = settings;
        _start = path ??= PropertyPath.Empty;
        _stopAtFirst = stopAtFirst;
        _depth = 0;
        Stopped = false;
        _paths[0] = path;
        var node = settings?.IgnoreRoot;
        if (node is not null)
        {
            foreach (var segment in path.Segments)
            {
                node = node.Child(segment);
                if (node is null) break;
            }
            if (node?.Ignored == true) return false;
        }
        _nodes[0] = node;
        return true;
    }

    void Clear()
    {
        System.Array.Clear(_values, 0, _depth + 1);
        System.Array.Clear(_nodes, 0, _depth + 1);
        System.Array.Clear(_paths, 0, _depth + 1);
        _depth = 0;
        _list = null;
        _settings = null;
        _start = PropertyPath.Empty;
    }

    PropertyPath Materialize(int depth)
    {
        var path = _paths[depth];
        if (path is not null) return path;
        return _paths[depth] = PropertyPath.Create(Materialize(depth - 1), _kinds[depth], _values[depth], _indices[depth]);
    }

    void Push(byte kind, object? value, int index, IgnoreNode? node)
    {
        var depth = ++_depth;
        if (depth == _kinds.Length)
        {
            var size = depth * 2;
            System.Array.Resize(ref _kinds, size);
            System.Array.Resize(ref _values, size);
            System.Array.Resize(ref _indices, size);
            System.Array.Resize(ref _nodes, size);
            System.Array.Resize(ref _paths, size);
        }
        _kinds[depth] = kind;
        _values[depth] = value;
        _indices[depth] = index;
        _nodes[depth] = node;
        _paths[depth] = null;
    }

    /// <summary>Enters a property segment. Returns false, pushing nothing, when the child path is ignored.</summary>
    public bool Enter(string property)
    {
        IgnoreNode? child = null;
        if (_nodes[_depth] is { } node)
        {
            child = node.Property(property);
            if (child?.Ignored == true) return false;
        }
        Push(PropertyPath.PropertyKind, property, 0, child);
        return true;
    }

    /// <summary>Enters a key segment. Returns false, pushing nothing, when the child path is ignored.</summary>
    public bool EnterKey(object key)
    {
        IgnoreNode? child = null;
        if (_nodes[_depth] is { } node)
        {
            child = node.Key(key);
            if (child?.Ignored == true) return false;
        }
        Push(PropertyPath.KeyKind, key, 0, child);
        return true;
    }

    /// <summary>Enters an index segment. Returns false, pushing nothing, when the child path is ignored.</summary>
    public bool EnterIndex(int index)
    {
        IgnoreNode? child = null;
        if (_nodes[_depth] is { } node)
        {
            child = node.Index(index);
            if (child?.Ignored == true) return false;
        }
        Push(PropertyPath.IndexKind, null, index, child);
        return true;
    }

    /// <summary>Leaves the segment entered last.</summary>
    public void Exit()
    {
        var depth = _depth;
        _values[depth] = null;
        _nodes[depth] = null;
        _paths[depth] = null;
        _depth = depth - 1;
    }

    bool Begin()
    {
        if (Stopped) return false;
        if (!_stopAtFirst) return true;
        Stopped = true;
        return false;
    }

    void Add(Difference difference) => (_list ??= []).Add(difference);

    /// <summary>Records a changed value at the current path.</summary>
    public void Changed(object? oldValue, object? newValue)
    {
        if (Begin()) Add(new(Path, oldValue, newValue));
    }

    /// <summary>Records a changed child property, unless its path is ignored.</summary>
    public void Changed<T>(string property, T oldValue, T newValue)
    {
        if (_nodes[_depth]?.Property(property)?.Ignored == true) return;
        if (Begin()) Add(new(Path.Push(property), oldValue, newValue));
    }

    /// <summary>Records an added value at the current path.</summary>
    public void Added(object? value)
    {
        if (Begin()) Add(Difference.Added(Path, value));
    }

    /// <summary>Records a removed value at the current path.</summary>
    public void Removed(object? value)
    {
        if (Begin()) Add(Difference.Removed(Path, value));
    }

    /// <summary>Adds the result of a custom comparison. In stop mode a non-empty result stops the walk.</summary>
    public void AddRange(ImmutableArray<Difference> differences)
    {
        if (differences.IsDefaultOrEmpty || Stopped) return;
        if (_stopAtFirst) Stopped = true;
        else (_list ??= []).AddRange(differences);
    }

    bool Precheck(object? oldValue, object? newValue)
    {
        if (Stopped || ReferenceEquals(oldValue, newValue)) return true;
        if (oldValue is null || newValue is null)
        {
            Changed(oldValue, newValue);
            return true;
        }
        return false;
    }

    /// <summary>Compares dictionaries by key at the current path; removed and added entries are reported as such.</summary>
    public void Dictionary<TKey, TValue>(IReadOnlyDictionary<TKey, TValue>? oldValue, IReadOnlyDictionary<TKey, TValue>? newValue, DiffWalk<TValue> walk) where TKey : notnull
    {
        if (Precheck(oldValue, newValue)) return;
        foreach (var pair in oldValue!)
        {
            if (Stopped) return;
            DictionaryOld(pair.Key, pair.Value, newValue!, walk);
        }
        foreach (var pair in newValue!)
        {
            if (Stopped) return;
            DictionaryNew(pair.Key, pair.Value, oldValue);
        }
    }

    /// <summary>Compares dictionaries by key, like the <see cref="IReadOnlyDictionary{TKey, TValue}"/> overload.</summary>
    public void Dictionary<TKey, TValue>(IDictionary<TKey, TValue>? oldValue, IDictionary<TKey, TValue>? newValue, DiffWalk<TValue> walk) where TKey : notnull
        => Dictionary(
            oldValue is null ? null : new ReadOnlyDictionaryAdapter<TKey, TValue>(oldValue),
            newValue is null ? null : new ReadOnlyDictionaryAdapter<TKey, TValue>(newValue),
            walk);

    /// <summary>Allocation-free overload for <see cref="ImmutableDictionary{TKey, TValue}"/>; call it with the exact static type.</summary>
    public void Dictionary<TKey, TValue>(ImmutableDictionary<TKey, TValue>? oldValue, ImmutableDictionary<TKey, TValue>? newValue, DiffWalk<TValue> walk) where TKey : notnull
    {
        if (Precheck(oldValue, newValue)) return;
        foreach (var pair in oldValue!)
        {
            if (Stopped) return;
            DictionaryOld(pair.Key, pair.Value, newValue!, walk);
        }
        foreach (var pair in newValue!)
        {
            if (Stopped) return;
            DictionaryNew(pair.Key, pair.Value, oldValue);
        }
    }

    /// <summary>Allocation-free overload for <see cref="System.Collections.Generic.Dictionary{TKey, TValue}"/>; call it with the exact static type.</summary>
    public void Dictionary<TKey, TValue>(Dictionary<TKey, TValue>? oldValue, Dictionary<TKey, TValue>? newValue, DiffWalk<TValue> walk) where TKey : notnull
    {
        if (Precheck(oldValue, newValue)) return;
        foreach (var pair in oldValue!)
        {
            if (Stopped) return;
            DictionaryOld(pair.Key, pair.Value, newValue!, walk);
        }
        foreach (var pair in newValue!)
        {
            if (Stopped) return;
            DictionaryNew(pair.Key, pair.Value, oldValue);
        }
    }

    void DictionaryOld<TKey, TValue>(TKey key, TValue value, IReadOnlyDictionary<TKey, TValue> newValue, DiffWalk<TValue> walk) where TKey : notnull
    {
        if (!EnterKey(key)) return;
        if (newValue.TryGetValue(key, out var updated)) walk(value, updated, this);
        else Removed(value);
        Exit();
    }

    void DictionaryNew<TKey, TValue>(TKey key, TValue value, IReadOnlyDictionary<TKey, TValue> oldValue) where TKey : notnull
    {
        if (oldValue.ContainsKey(key) || !EnterKey(key)) return;
        Added(value);
        Exit();
    }

    /// <summary>Compares sets by membership; differences are reported at the current path.</summary>
    public void Set<T>(IReadOnlySet<T>? oldValue, IReadOnlySet<T>? newValue)
    {
        if (Precheck(oldValue, newValue) || oldValue!.SetEquals(newValue!)) return;
        foreach (var item in oldValue)
        {
            if (Stopped) return;
            if (!newValue!.Contains(item)) Removed(item);
        }
        foreach (var item in newValue!)
        {
            if (Stopped) return;
            if (!oldValue.Contains(item)) Added(item);
        }
    }

    /// <summary>Compares sets by membership; differences are reported at the current path.</summary>
    public void Set<T>(ISet<T>? oldValue, ISet<T>? newValue)
    {
        if (Precheck(oldValue, newValue) || oldValue!.SetEquals(newValue!)) return;
        foreach (var item in oldValue)
        {
            if (Stopped) return;
            if (!newValue!.Contains(item)) Removed(item);
        }
        foreach (var item in newValue!)
        {
            if (Stopped) return;
            if (!oldValue.Contains(item)) Added(item);
        }
    }

    /// <summary>
    /// Compares collections matched by key, ignoring order. Throws <see cref="InvalidOperationException"/> for null elements, null keys or duplicate keys.
    /// Sides with the same references in the same order are skipped without validation.
    /// </summary>
    public void Keyed<T, TKey>(IEnumerable<T>? oldValue, IEnumerable<T>? newValue, Func<T, TKey> key, DiffWalk<T> walk) where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(walk);
        if (Precheck(oldValue, newValue) || SameElements(oldValue!, newValue!)) return;

        var oldItems = Index(oldValue!, key);
        var newItems = Index(newValue!, key);
        foreach (var (k, item) in oldItems)
        {
            if (Stopped) return;
            if (!EnterKey(k)) continue;
            if (newItems.TryGetValue(k, out var updated)) walk(item, updated, this);
            else Removed(item);
            Exit();
        }
        foreach (var (k, item) in newItems)
        {
            if (Stopped) return;
            if (oldItems.ContainsKey(k) || !EnterKey(k)) continue;
            Added(item);
            Exit();
        }
    }

    static bool SameElements<T>(IEnumerable<T> oldValue, IEnumerable<T> newValue)
    {
        if (typeof(T).IsValueType || oldValue is not IReadOnlyList<T> o || newValue is not IReadOnlyList<T> n || o.Count != n.Count) return false;
        for (var i = 0; i < o.Count; i++)
            if (!ReferenceEquals(o[i], n[i])) return false;
        return true;
    }

    Dictionary<TKey, T> Index<T, TKey>(IEnumerable<T> items, Func<T, TKey> key) where TKey : notnull
    {
        var result = new Dictionary<TKey, T>();
        foreach (var item in items)
        {
            if (item is null) throw new InvalidOperationException($"Null element at '{Path}'.");
            var k = key(item);
            if (k is null) throw new InvalidOperationException($"Null key at '{Path}'.");
            if (!result.TryAdd(k, item)) throw new InvalidOperationException($"Duplicate key '{k}' at '{Path}'.");
        }
        return result;
    }

    /// <summary>Compares sequences by position; positions past the shorter sequence are reported as added or removed. With <paramref name="atomicElements"/>, the sequences are compared as one value.</summary>
    public void Enumerable<T>(IEnumerable<T>? oldValue, IEnumerable<T>? newValue, DiffWalk<T> walk, bool atomicElements)
    {
        ArgumentNullException.ThrowIfNull(walk);
        if (Precheck(oldValue, newValue)) return;
        if (atomicElements)
        {
            if (!oldValue!.SequenceEqual(newValue!)) Changed(oldValue, newValue);
            return;
        }
        using var oldItems = oldValue!.GetEnumerator();
        using var newItems = newValue!.GetEnumerator();
        for (var index = 0; ; index++)
        {
            var hasOld = oldItems.MoveNext();
            var hasNew = newItems.MoveNext();
            if (!hasOld && !hasNew) break;
            if (Stopped) return;
            if (!EnterIndex(index)) continue;
            if (!hasOld) Added(newItems.Current);
            else if (!hasNew) Removed(oldItems.Current);
            else walk(oldItems.Current, newItems.Current, this);
            Exit();
        }
    }

    /// <summary>Allocation-free positional comparison for <see cref="ImmutableArray{T}"/>; the arrays must not be default.</summary>
    public void Array<T>(ImmutableArray<T> oldValue, ImmutableArray<T> newValue, DiffWalk<T> walk, bool atomicElements)
    {
        ArgumentNullException.ThrowIfNull(walk);
        if (Stopped || oldValue == newValue) return;
        if (Positional(oldValue.AsSpan(), newValue.AsSpan(), walk, atomicElements)) Changed(oldValue, newValue);
    }

    /// <summary>Allocation-free positional comparison for arrays.</summary>
    public void Array<T>(T[]? oldValue, T[]? newValue, DiffWalk<T> walk, bool atomicElements)
    {
        ArgumentNullException.ThrowIfNull(walk);
        if (Precheck(oldValue, newValue)) return;
        if (Positional(oldValue, newValue, walk, atomicElements)) Changed(oldValue, newValue);
    }

    /// <summary>Allocation-free positional comparison for <see cref="IReadOnlyList{T}"/> (index loops).</summary>
    public void List<T>(IReadOnlyList<T>? oldValue, IReadOnlyList<T>? newValue, DiffWalk<T> walk, bool atomicElements)
    {
        ArgumentNullException.ThrowIfNull(walk);
        if (Precheck(oldValue, newValue)) return;
        var oldCount = oldValue!.Count;
        var newCount = newValue!.Count;
        if (atomicElements)
        {
            var equal = oldCount == newCount;
            for (var i = 0; equal && i < oldCount; i++)
                equal = EqualityComparer<T>.Default.Equals(oldValue[i], newValue[i]);
            if (!equal) Changed(oldValue, newValue);
            return;
        }
        for (var i = 0; i < Math.Max(oldCount, newCount); i++)
        {
            if (Stopped) return;
            if (!EnterIndex(i)) continue;
            if (i >= oldCount) Added(newValue[i]);
            else if (i >= newCount) Removed(oldValue[i]);
            else walk(oldValue[i], newValue[i], this);
            Exit();
        }
    }

    bool Positional<T>(ReadOnlySpan<T> oldValue, ReadOnlySpan<T> newValue, DiffWalk<T> walk, bool atomicElements)
    {
        if (atomicElements) return !oldValue.SequenceEqual(newValue);
        for (var i = 0; i < Math.Max(oldValue.Length, newValue.Length); i++)
        {
            if (Stopped) return false;
            if (!EnterIndex(i)) continue;
            if (i >= oldValue.Length) Added(newValue[i]);
            else if (i >= newValue.Length) Removed(oldValue[i]);
            else walk(oldValue[i], newValue[i], this);
            Exit();
        }
        return false;
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
