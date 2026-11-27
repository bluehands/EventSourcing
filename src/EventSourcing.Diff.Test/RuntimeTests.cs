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
            (IReadOnlyDictionary<string, string>)new Dictionary<string, string> { ["removed"] = "A", ["updated"] = "B" },
            (IReadOnlyDictionary<string, string>)new Dictionary<string, string> { ["added"] = "C", ["updated"] = "D" }, Compare, path, settings);
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

    static readonly PropertyPath Lines = PropertyPath.Empty.Push("Order").Push("Lines");

    [Fact]
    public void Dictionary_reports_added_and_removed_kinds_and_null_values_are_distinguishable()
    {
        var result = CollectionDiff.Dictionary<string, string?>(
            (IReadOnlyDictionary<string, string?>)new Dictionary<string, string?> { ["gone"] = null },
            (IReadOnlyDictionary<string, string?>)new Dictionary<string, string?> { ["new"] = null }, CompareNullable);
        Assert.Equal([Difference.Removed(PropertyPath.Empty.PushKey("gone"), null), Difference.Added(PropertyPath.Empty.PushKey("new"), null)], result);
        Assert.Equal(DifferenceKind.Removed, result[0].Kind);
        Assert.Equal(DifferenceKind.Added, result[1].Kind);
    }

    [Fact]
    public void Enumerable_reports_added_and_removed_kinds()
    {
        var added = CollectionDiff.Enumerable(["a"], ["a", "b"], Compare, false);
        Assert.Equal([Difference.Added(PropertyPath.Empty.PushIndex(1), "b")], added);
        var removed = CollectionDiff.Enumerable(["a", "b"], ["a"], Compare, false);
        Assert.Equal([Difference.Removed(PropertyPath.Empty.PushIndex(1), "b")], removed);
    }

    [Fact]
    public void IDictionary_overload_ignores_order_and_reports_per_key_changes()
    {
        var oldValue = (IDictionary<string, string>)new Dictionary<string, string> { ["a"] = "1", ["b"] = "2" };
        var reordered = (IDictionary<string, string>)new Dictionary<string, string> { ["b"] = "2", ["a"] = "1" };
        Assert.Empty(CollectionDiff.Dictionary(oldValue, reordered, Compare));
        var changed = (IDictionary<string, string>)new Dictionary<string, string> { ["a"] = "1", ["b"] = "3" };
        var result = CollectionDiff.Dictionary(oldValue, changed, Compare);
        Assert.Equal([new Difference(PropertyPath.Empty.PushKey("b"), "2", "3")], result);
        Assert.Single(CollectionDiff.Dictionary(null, oldValue, Compare));
    }

    [Fact]
    public void Set_ignores_order_and_reports_added_and_removed_elements()
    {
        Assert.Empty(CollectionDiff.Set((IReadOnlySet<string>)new HashSet<string> { "a", "b" }, new HashSet<string> { "b", "a" }));
        Assert.Empty(CollectionDiff.Set((ISet<string>)new HashSet<string> { "a", "b" }, new HashSet<string> { "b", "a" }));

        var path = PropertyPath.Empty.Push("Tags");
        var result = CollectionDiff.Set((ISet<string>)new HashSet<string> { "a", "b" }, new HashSet<string> { "b", "c" }, path);
        Assert.Equal([Difference.Removed(path, "a"), Difference.Added(path, "c")], result);
        var readOnly = CollectionDiff.Set((IReadOnlySet<string>)new HashSet<string> { "a", "b" }, new HashSet<string> { "b", "c" }, path);
        Assert.Equal(result, readOnly);
    }

    [Fact]
    public void Set_null_transitions_and_ignored_path()
    {
        var path = PropertyPath.Empty.Push("Tags");
        var set = new HashSet<string> { "a" };
        var result = CollectionDiff.Set(null, (ISet<string>)set, path);
        Assert.Equal([new Difference(path, null, set)], result);
        Assert.Equal(DifferenceKind.Changed, result[0].Kind);
        Assert.Single(CollectionDiff.Set((IReadOnlySet<string>)set, null, path));
        Assert.Empty(CollectionDiff.Set((ISet<string>)set, new HashSet<string>(), path, new DiffSettings([path])));
        Assert.Empty(CollectionDiff.Set((ISet<string>)set, set, path));
    }

    [Fact]
    public void Keyed_ignores_order()
        => Assert.Empty(CollectionDiff.Keyed(["a1", "b2"], ["b2", "a1"], KeyOf, Compare, Lines));

    [Fact]
    public void Keyed_insertion_reports_only_added_at_its_key()
    {
        var result = CollectionDiff.Keyed(["a1", "b2"], ["a1", "c3", "b2"], KeyOf, Compare, Lines);
        Assert.Equal([Difference.Added(Lines.PushKey("c"), "c3")], result);
    }

    [Fact]
    public void Keyed_removal_and_matched_change_use_keyed_paths()
    {
        var result = CollectionDiff.Keyed(["a1", "b2"], ["a9"], KeyOf, Compare, Lines);
        Assert.Equal([new Difference(Lines.PushKey("a"), "a1", "a9"), Difference.Removed(Lines.PushKey("b"), "b2")], result);
    }

    [Fact]
    public void Keyed_null_transitions_and_ignored_root()
    {
        Assert.Single(CollectionDiff.Keyed(null, ["a1"], KeyOf, Compare, Lines));
        Assert.Empty(CollectionDiff.Keyed(null, ["a1"], KeyOf, Compare, Lines, new DiffSettings([Lines])));
    }

    [Fact]
    public void Keyed_throws_for_duplicate_keys_null_elements_and_null_keys()
    {
        var duplicate = Assert.Throws<InvalidOperationException>(() => CollectionDiff.Keyed(["a1", "a2"], ["a1"], KeyOf, Compare, Lines));
        Assert.Contains("Duplicate key 'a' at 'Order.Lines'", duplicate.Message);
        var nullElement = Assert.Throws<InvalidOperationException>(() => CollectionDiff.Keyed(["a1"], [null!], KeyOf, Compare, Lines));
        Assert.Contains("Order.Lines", nullElement.Message);
        var nullKey = Assert.Throws<InvalidOperationException>(() => CollectionDiff.Keyed(["a1"], ["x"], s => s == "x" ? null! : s, Compare, Lines));
        Assert.Contains("Order.Lines", nullKey.Message);
    }

    [Fact]
    public void Keyed_ignores_apply_to_added_removed_and_modified_entries()
    {
        var settings = new DiffSettings([Lines.PushKey("a"), Lines.PushKey("b"), Lines.PushKey("c")]);
        Assert.Empty(CollectionDiff.Keyed(["a1", "b2"], ["a9", "c3"], KeyOf, Compare, Lines, settings));
    }

    static string KeyOf(string value) => value[..1];

    static ImmutableArray<Difference> CompareNullable(string? oldValue, string? newValue, PropertyPath? path, DiffSettings? settings)
        => oldValue == newValue ? [] : [new(path ?? PropertyPath.Empty, oldValue, newValue)];

    static ImmutableArray<Difference> Compare(string oldValue, string newValue, PropertyPath? path, DiffSettings? settings)
        => oldValue == newValue ? [] : [new(path ?? PropertyPath.Empty, oldValue, newValue)];
}
