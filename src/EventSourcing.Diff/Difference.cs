namespace EventSourcing.Diff;

public sealed record Difference(PropertyPath PropertyPath, object? OldValue, object? NewValue);
