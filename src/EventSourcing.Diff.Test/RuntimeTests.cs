using System.Collections.Immutable;

namespace EventSourcing.Diff.Test;

public class RuntimeTests
{
    [Fact]
    public void Paths_have_structural_equality_and_typed_segments()
    {
        var path = PropertyPath.Empty.Push("Items").PushKey(42).Push("Name");
        Assert.Equal(path, PropertyPath.Empty.Push("Items").PushKey(42).Push("Name"));
        Assert.Equal(path.GetHashCode(), PropertyPath.Empty.Push("Items").PushKey(42).Push("Name").GetHashCode());
        Assert.NotEqual(path, PropertyPath.Empty.Push("Items").PushIndex(42).Push("Name"));
        Assert.Equal("Items[42].Name", path.ToString());
        Assert.Equal(PropertyPath.Empty.Push("Items").PushKey(42), path.Pop());
        Assert.Throws<InvalidOperationException>(() => PropertyPath.Empty.Pop());
    }

    [Fact]
    public void Dictionary_ignores_apply_to_added_removed_and_modified_entries()
    {
        var path = PropertyPath.Empty.Push("Entries");
        var settings = new DiffSettings([path.PushKey("removed"), path.PushKey("added"), path.PushKey("updated")]);
        var result = CollectionDiff.Dictionary<string, string>(
            new Dictionary<string, string> { ["removed"] = "A", ["updated"] = "B" },
            new Dictionary<string, string> { ["added"] = "C", ["updated"] = "D" }, Compare, path, settings);
        Assert.Empty(result);
    }

    [Fact]
    public void Collection_ignores_apply_to_added_and_removed_indices()
    {
        var settings = new DiffSettings([PropertyPath.Empty.PushIndex(0)]);
        Assert.Empty(CollectionDiff.Enumerable(Array.Empty<string>(), ["new"], Compare, false, settings: settings));
        Assert.Empty(CollectionDiff.Enumerable(["old"], Array.Empty<string>(), Compare, false, settings: settings));
    }

    [Fact]
    public void Atomic_collections_report_one_whole_collection_difference()
    {
        var result = CollectionDiff.Enumerable(["a", "b"], ["b", "a"], Compare, true);
        Assert.Single(result);
        Assert.Equal(PropertyPath.Empty, result[0].PropertyPath);
    }

    [Fact]
    public void Null_transitions_report_one_difference_and_root_can_be_ignored()
    {
        Assert.Single(CollectionDiff.Enumerable<string>(null, ["new"], Compare, false));
        Assert.Empty(CollectionDiff.Enumerable<string>(null, ["new"], Compare, false,
            settings: new DiffSettings([PropertyPath.Empty])));
    }

    static ImmutableArray<Difference> Compare(string oldValue, string newValue, PropertyPath? path, DiffSettings? settings)
        => oldValue == newValue ? [] : [new(path ?? PropertyPath.Empty, oldValue, newValue)];
}
