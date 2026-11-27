using System.Collections.Immutable;

namespace EventSourcing.Diff;

/// <summary>Runtime options for generated comparisons.</summary>
/// <param name="pathsToIgnore">Exact paths at which comparison stops, including all descendants.</param>
public sealed class DiffSettings(IEnumerable<PropertyPath>? pathsToIgnore = null)
{
    /// <summary>Exact paths at which comparison stops, including all descendants.</summary>
    public ImmutableHashSet<PropertyPath> PathsToIgnore { get; } = pathsToIgnore?.ToImmutableHashSet() ?? [];
}
