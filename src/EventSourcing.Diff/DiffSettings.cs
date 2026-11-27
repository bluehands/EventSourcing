using System.Collections.Immutable;

namespace EventSourcing.Diff;

public sealed class DiffSettings(IEnumerable<PropertyPath>? pathsToIgnore = null)
{
    /// <summary>Exact paths at which comparison stops, including all descendants.</summary>
    public ImmutableHashSet<PropertyPath> PathsToIgnore { get; } = pathsToIgnore?.ToImmutableHashSet() ?? [];
}
