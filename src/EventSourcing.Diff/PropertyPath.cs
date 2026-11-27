using System.Collections.Immutable;
using System.Runtime.InteropServices;
using System.Globalization;
using System.Text;

namespace EventSourcing.Diff;

/// <summary>One step of a <see cref="PropertyPath"/>.</summary>
public abstract record PathSegment
{
    /// <summary>A property access.</summary>
    /// <param name="Name">The property name.</param>
    public sealed record Property(string Name) : PathSegment;
    /// <summary>A dictionary or keyed-collection entry.</summary>
    /// <param name="Value">The key.</param>
    public sealed record Key(object Value) : PathSegment;
    /// <summary>A position in a sequence.</summary>
    /// <param name="Value">The zero-based index.</param>
    public sealed record Index(int Value) : PathSegment;
}

/// <summary>An immutable, structurally comparable path of property, key and index segments.</summary>
public sealed class PropertyPath : IEquatable<PropertyPath>
{
    internal const byte PropertyKind = 0, KeyKind = 1, IndexKind = 2;

    readonly PropertyPath? _parent;
    readonly byte _kind;
    readonly object? _value;
    readonly int _index;
    readonly int _depth;
    readonly int _hash;
    ImmutableArray<PathSegment> _segments;

    /// <summary>The root path with no segments.</summary>
    public static PropertyPath Empty { get; } = new();

    PropertyPath() => _segments = [];

    PropertyPath(PropertyPath parent, byte kind, object? value, int index)
    {
        _parent = parent;
        _kind = kind;
        _value = value;
        _index = index;
        _depth = parent._depth + 1;
        var segmentHash = kind == IndexKind ? index : value!.GetHashCode();
        _hash = HashCode.Combine(parent._hash, kind, segmentHash);
    }

    internal static PropertyPath Create(PropertyPath parent, byte kind, object? value, int index) => new(parent, kind, value, index);

    /// <summary>The path segments from the root.</summary>
    public ImmutableArray<PathSegment> Segments
    {
        get
        {
            var segments = _segments;
            if (!segments.IsDefault) return segments;
            var array = new PathSegment[_depth];
            var node = this;
            for (var i = _depth - 1; i >= 0; i--, node = node._parent!)
                array[i] = node._kind switch
                {
                    PropertyKind => new PathSegment.Property((string)node._value!),
                    KeyKind => new PathSegment.Key(node._value!),
                    _ => new PathSegment.Index(node._index)
                };
            return _segments = ImmutableCollectionsMarshal.AsImmutableArray(array);
        }
    }

    /// <summary>Returns a path extended by a property segment.</summary>
    public PropertyPath Push(string propertyName)
    {
        ArgumentException.ThrowIfNullOrEmpty(propertyName);
        return new(this, PropertyKind, propertyName, 0);
    }

    /// <summary>Returns a path extended by a key segment.</summary>
    public PropertyPath PushKey(object key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return new(this, KeyKind, key, 0);
    }

    /// <summary>Returns a path extended by an index segment.</summary>
    public PropertyPath PushIndex(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        return new(this, IndexKind, null, index);
    }

    /// <summary>Returns a path without its last segment.</summary>
    public PropertyPath Pop() => _parent ?? throw new InvalidOperationException("Cannot pop an empty property path.");

    /// <inheritdoc/>
    public bool Equals(PropertyPath? other)
    {
        if (ReferenceEquals(this, other)) return true;
        if (other is null || _depth != other._depth || _hash != other._hash) return false;
        PropertyPath? a = this, b = other;
        while (a is not null && b is not null)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a._kind != b._kind) return false;
            if (a._kind == IndexKind ? a._index != b._index : !Equals(a._value, b._value)) return false;
            a = a._parent;
            b = b._parent;
        }
        return true;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is PropertyPath other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => _hash;

    /// <summary>A display string, not a serialization format. Keys and indices remain distinct in <see cref="Segments"/>.</summary>
    public override string ToString()
    {
        var result = new StringBuilder();
        foreach (var segment in Segments)
        {
            switch (segment)
            {
                case PathSegment.Property property:
                    if (result.Length > 0) result.Append('.');
                    result.Append(property.Name);
                    break;
                case PathSegment.Key key:
                    result.Append('[').Append(Convert.ToString(key.Value, CultureInfo.InvariantCulture)).Append(']');
                    break;
                case PathSegment.Index index:
                    result.Append('[').Append(index.Value.ToString(CultureInfo.InvariantCulture)).Append(']');
                    break;
            }
        }
        return result.ToString();
    }
}