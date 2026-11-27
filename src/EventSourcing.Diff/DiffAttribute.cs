namespace EventSourcing.Diff;

/// <summary>
/// Generates <c>Diff</c> overloads for <paramref name="typeToDiff"/> and all types reachable from it in the annotated
/// top-level, non-generic static partial class.
/// </summary>
/// <param name="typeToDiff">The model type to compare.</param>
/// <param name="propertiesToIgnore">Property names excluded from comparisons of this type, its subclasses and implementations.</param>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class DiffAttribute(Type typeToDiff, params string[] propertiesToIgnore) : Attribute
{
    /// <summary>The model type to compare.</summary>
    public Type TypeToDiff { get; } = typeToDiff;

    /// <summary>Property names excluded from comparisons of this type, its subclasses and implementations.</summary>
    public string[] PropertiesToIgnore { get; } = propertiesToIgnore;

    /// <summary>Compare this type, its subclasses and implementations using equality instead of traversing properties.</summary>
    public bool TreatAsValue { get; set; }

    /// <summary>
    /// Name of a property identifying elements of this type, its subclasses and implementations in lists and sets.
    /// Elements are then matched by key instead of position, so reordering produces no differences.
    /// </summary>
    public string? MatchBy { get; set; }
}
