namespace EventSourcing.Diff;

/// <summary>Describes how a value differs between the old and the new model.</summary>
public enum DifferenceKind
{
    /// <summary>A value changed, including transitions to or from null and runtime-type changes.</summary>
    Changed,

    /// <summary>A dictionary entry, collection item or set element exists only in the new value.</summary>
    Added,

    /// <summary>A dictionary entry, collection item or set element exists only in the old value.</summary>
    Removed
}

/// <summary>A single difference at <paramref name="PropertyPath"/>. Values are references to the compared models, not snapshots.</summary>
/// <param name="PropertyPath">Location of the difference, relative to the compared root.</param>
/// <param name="OldValue">The old value; null for <see cref="DifferenceKind.Added"/>.</param>
/// <param name="NewValue">The new value; null for <see cref="DifferenceKind.Removed"/>.</param>
/// <param name="Kind">Whether the value changed or a collection member was added or removed.</param>
public sealed record Difference(PropertyPath PropertyPath, object? OldValue, object? NewValue, DifferenceKind Kind = DifferenceKind.Changed)
{
    /// <summary>Creates a <see cref="DifferenceKind.Changed"/> difference.</summary>
    public static Difference Changed(PropertyPath path, object? oldValue, object? newValue) => new(path, oldValue, newValue);

    /// <summary>Creates a <see cref="DifferenceKind.Added"/> difference for a collection member.</summary>
    public static Difference Added(PropertyPath path, object? value) => new(path, null, value, DifferenceKind.Added);

    /// <summary>Creates a <see cref="DifferenceKind.Removed"/> difference for a collection member.</summary>
    public static Difference Removed(PropertyPath path, object? value) => new(path, value, null, DifferenceKind.Removed);
}
