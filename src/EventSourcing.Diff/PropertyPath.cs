using System.Collections.Immutable;
using System.Globalization;
using System.Text;

namespace EventSourcing.Diff;

public abstract record PathSegment
{
    public sealed record Property(string Name) : PathSegment;
    public sealed record Key(object Value) : PathSegment;
    public sealed record Index(int Value) : PathSegment;
}

public sealed class PropertyPath : IEquatable<PropertyPath>
{
    public static PropertyPath Empty { get; } = new([]);

    PropertyPath(ImmutableArray<PathSegment> segments) => Segments = segments;

    public ImmutableArray<PathSegment> Segments { get; }

    public PropertyPath Push(string propertyName)
    {
        ArgumentException.ThrowIfNullOrEmpty(propertyName);
        return new(Segments.Add(new PathSegment.Property(propertyName)));
    }

    public PropertyPath PushKey(object key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return new(Segments.Add(new PathSegment.Key(key)));
    }

    public PropertyPath PushIndex(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        return new(Segments.Add(new PathSegment.Index(index)));
    }

    public PropertyPath Pop() => Segments.IsEmpty
        ? throw new InvalidOperationException("Cannot pop an empty property path.")
        : new(Segments.RemoveAt(Segments.Length - 1));

    public bool Equals(PropertyPath? other) => ReferenceEquals(this, other)
        || other is not null && Segments.SequenceEqual(other.Segments);

    public override bool Equals(object? obj) => obj is PropertyPath other && Equals(other);

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
