namespace EventSourcing.Diff;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class DiffAttribute(Type typeToDiff, params string[] propertiesToIgnore) : Attribute
{
    public Type TypeToDiff { get; } = typeToDiff;
    public string[] PropertiesToIgnore { get; } = propertiesToIgnore;

    /// <summary>Compare this type, its subclasses and implementations using equality instead of traversing properties.</summary>
    public bool TreatAsValue { get; set; }
}
