namespace EventSourcing.Diff;

/// <summary>Trie node over the segments of ignored paths.</summary>
internal sealed class IgnoreNode
{
    Dictionary<string, IgnoreNode>? _properties;
    Dictionary<object, IgnoreNode>? _keys;
    Dictionary<int, IgnoreNode>? _indices;

    public bool Ignored { get; private set; }

    public IgnoreNode? Property(string name) => _properties is not null && _properties.TryGetValue(name, out var node) ? node : null;

    public IgnoreNode? Key(object key) => _keys is not null && _keys.TryGetValue(key, out var node) ? node : null;

    public IgnoreNode? Index(int index) => _indices is not null && _indices.TryGetValue(index, out var node) ? node : null;

    public IgnoreNode? Child(PathSegment segment) => segment switch
    {
        PathSegment.Property p => Property(p.Name),
        PathSegment.Key k => Key(k.Value),
        PathSegment.Index i => Index(i.Value),
        _ => null
    };

    public static IgnoreNode? Build(IEnumerable<PropertyPath> paths)
    {
        IgnoreNode? root = null;
        foreach (var path in paths)
        {
            root ??= new();
            var node = root;
            foreach (var segment in path.Segments) node = node.GetOrAdd(segment);
            node.Ignored = true;
        }
        return root;
    }

    IgnoreNode GetOrAdd(PathSegment segment)
    {
        switch (segment)
        {
            case PathSegment.Property p:
                _properties ??= new(StringComparer.Ordinal);
                if (!_properties.TryGetValue(p.Name, out var property)) _properties[p.Name] = property = new();
                return property;
            case PathSegment.Key k:
                _keys ??= [];
                if (!_keys.TryGetValue(k.Value, out var key)) _keys[k.Value] = key = new();
                return key;
            case PathSegment.Index i:
                _indices ??= [];
                if (!_indices.TryGetValue(i.Value, out var index)) _indices[i.Value] = index = new();
                return index;
            default:
                throw new InvalidOperationException("Unknown path segment.");
        }
    }
}
