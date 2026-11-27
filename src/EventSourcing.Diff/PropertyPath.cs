using System.Collections.Immutable;
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
    /// <summary>The root path with no segments.</summary>
    public static PropertyPath Empty { get; } = new([]);

    PropertyPath(ImmutableArray<PathSegment> segments) => Segments = segments;

    /// <summary>The path segments from the root.</summary>
    public ImmutableArray<PathSegment> Segments { get; }

    /// <summary>Returns a path extended by a property segment.</summary>
    public PropertyPath Push(string propertyName)
    {
        ArgumentException.ThrowIfNullOrEmpty(propertyName);
        return new(Segments.Add(new PathSegment.Property(propertyName)));
    }

    /// <summary>Returns a path extended by a key segment.</summary>
    public PropertyPath PushKey(object key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return new(Segments.Add(new PathSegment.Key(key)));
    }

    /// <summary>Returns a path extended by an index segment.</summary>
    public PropertyPath PushIndex(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        return new(Segments.Add(new PathSegment.Index(index)));
    }

    /// <summary>Returns a path without its last segment.</summary>
    public PropertyPath Pop() => Segments.IsEmpty
        ? throw new InvalidOperationException("Cannot pop an empty property path.")
        : new(Segments.RemoveAt(Segments.Length - 1));

    /// <inheritdoc/>
    public bool Equals(PropertyPath? other) => ReferenceEquals(this, other)
        || other is not null && Segments.SequenceEqual(other.Segments);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is PropertyPath other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var segment in Segments) hash.Add(segment);
        return hash.ToHashCode();
    }

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
