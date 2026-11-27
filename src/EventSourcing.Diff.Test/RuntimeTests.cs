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

    static PropertyPath[] PathRoutes() =>
    [
        PropertyPath.Empty.Push("A").PushKey("k").PushIndex(2),
        PropertyPath.Empty.Push("A").PushKey("k").PushIndex(2).Push("B").Pop(),
        PropertyPath.Empty.Push("A").PushKey("k").PushIndex(2).Push("B").Pop().Pop().PushIndex(3),
    ];

    [Fact]
    public void Paths_built_by_different_routes_are_equal_with_equal_hashes_and_segments()
    {
        var routes = PathRoutes();
        Assert.Equal(routes[0], routes[1]);
        Assert.Equal(routes[0].GetHashCode(), routes[1].GetHashCode());
        Assert.Equal("A[k][2]", routes[0].ToString());
        Assert.Equal(routes[0].Segments, routes[1].Segments);
        Assert.Equal([new PathSegment.Property("A"), new PathSegment.Key("k"), new PathSegment.Index(2)], routes[0].Segments);
        Assert.NotEqual(routes[0], routes[2]);
        Assert.NotEqual(routes[0], PropertyPath.Empty.Push("A").PushKey("k").PushKey(2));
        Assert.NotEqual(routes[0], PropertyPath.Empty.Push("A").PushKey("x").PushIndex(2));
        Assert.True(PropertyPath.Empty.Segments.IsEmpty);
        Assert.Equal(PropertyPath.Empty, routes[0].Pop().Pop().Pop());
    }

    static void ObjectWalk(string? oldValue, string? newValue, DiffCollector c)
    {
        c.Changed("Total", 1, 2);
        if (c.Enter("Lines"))
        {
            c.Changed("Qty", 1, 2);
            c.Exit();
        }
    }

    static string[] Paths(ImmutableArray<Difference> differences) => [.. differences.Select(d => d.PropertyPath.ToString())];

    [Fact]
    public void Collector_collects_all_differences_and_has_differences_stops_at_first()
    {
        Assert.Equal(["Total", "Lines.Qty"], Paths(DiffCollector.Collect<string?>(null, null, null, null, ObjectWalk)));
        Assert.True(DiffCollector.HasDifferences<string?>(null, null, null, null, ObjectWalk));
        Assert.False(DiffCollector.HasDifferences<string?>(null, null, null, null, static (_, _, _) => { }));
        Assert.Empty(DiffCollector.Collect<string?>(null, null, null, null, static (_, _, _) => { }));
    }

    [Fact]
    public void Stop_mode_stops_loops_after_the_first_difference()
    {
        var calls = 0;
        var found = DiffCollector.HasDifferences<int[]>([1, 2, 3, 4], [9, 9, 9, 9], null, null,
            (o, n, c) => c.Array(o, n, (a, b, cc) => { calls++; if (a != b) cc.Changed(a, b); }, false));
        Assert.True(found);
        Assert.Equal(1, calls);

        calls = 0;
        found = DiffCollector.HasDifferences<IReadOnlyDictionary<string, int>>(
            new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 }, new Dictionary<string, int> { ["a"] = 5, ["b"] = 6 }, null, null,
            (o, n, c) => c.Dictionary(o, n, (a, b, cc) => { calls++; if (a != b) cc.Changed(a, b); }));
        Assert.True(found);
        Assert.Equal(1, calls);

        Assert.True(DiffCollector.HasDifferences<int[]>([1], [1, 2], null, null,
            (o, n, c) => c.Array(o, n, static (a, b, cc) => { }, false)));
        Assert.True(DiffCollector.HasDifferences<string?>(null, null, null, null,
            static (_, _, c) => c.AddRange([new Difference(PropertyPath.Empty, 1, 2)])));
    }

    [Fact]
    public void Ignored_paths_skip_the_node_and_its_descendants()
    {
        var lines = PropertyPath.Empty.Push("Lines");
        Assert.Equal(["Total"], Paths(DiffCollector.Collect<string?>(null, null, null, new DiffSettings([lines]), ObjectWalk)));
        Assert.Equal(["Lines.Qty"], Paths(DiffCollector.Collect<string?>(null, null, null, new DiffSettings([PropertyPath.Empty.Push("Total")]), ObjectWalk)));
        Assert.Equal(["Total"], Paths(DiffCollector.Collect<string?>(null, null, null, new DiffSettings([lines.Push("Qty")]), ObjectWalk)));
        Assert.True(DiffCollector.HasDifferences<string?>(null, null, null, new DiffSettings([lines]), ObjectWalk));
        Assert.False(DiffCollector.HasDifferences<string?>(null, null, null,
            new DiffSettings([lines, PropertyPath.Empty.Push("Total")]), ObjectWalk));
    }

    [Fact]
    public void Start_path_itself_ignored_yields_nothing_but_an_ignored_prefix_does_not()
    {
        var start = PropertyPath.Empty.Push("Order").Push("Lines");
        var walked = false;
        Assert.Empty(DiffCollector.Collect<string?>(null, null, start, new DiffSettings([start]),
            (_, _, _) => walked = true));
        Assert.False(walked);
        Assert.Equal(["Order.Lines.Total", "Order.Lines.Lines.Qty"],
            Paths(DiffCollector.Collect<string?>(null, null, start, new DiffSettings([PropertyPath.Empty.Push("Order")]), ObjectWalk)));
        Assert.Equal(["Order.Lines.Total"],
            Paths(DiffCollector.Collect<string?>(null, null, start, new DiffSettings([start.Push("Lines")]), ObjectWalk)));
    }

    [Fact]
    public void Key_and_index_ignores_apply_in_collector_collections()
    {
        var settings = new DiffSettings([PropertyPath.Empty.PushKey("b"), PropertyPath.Empty.PushIndex(1)]);
        var dictionary = DiffCollector.Collect<Dictionary<string, int>?>(
            new() { ["a"] = 1, ["b"] = 1 }, new() { ["a"] = 2, ["b"] = 2 }, null, settings,
            static (o, n, c) => c.Dictionary(o, n, static (a, b, cc) => { if (a != b) cc.Changed(a, b); }));
        Assert.Equal(["[a]"], Paths(dictionary));
        var items = DiffCollector.Collect<ImmutableArray<int>>([1, 1, 1], [2, 2, 2], null, settings,
            static (o, n, c) => c.Array(o, n, static (a, b, cc) => { if (a != b) cc.Changed(a, b); }, false));
        Assert.Equal(["[0]", "[2]"], Paths(items));
    }

    [Fact]
    public void Nested_root_calls_from_a_walk_work_and_do_not_disturb_the_outer_collector()
    {
        var result = DiffCollector.Collect<string?>(null, null, null, null, static (_, _, c) =>
        {
            c.Changed("Before", 1, 2);
            if (c.Enter("Child"))
            {
                c.AddRange(DiffCollector.Collect<string?>(null, null, c.Path, c.Settings, ObjectWalk));
                c.Exit();
            }
            c.Changed("After", 1, 2);
        });
        Assert.Equal(["Before", "Child.Total", "Child.Lines.Qty", "After"], Paths(result));
    }

    [Fact]
    public void Exception_in_a_walk_does_not_poison_the_next_call()
    {
        Assert.Throws<InvalidOperationException>(() => DiffCollector.Collect<string?>(null, null, null,
            new DiffSettings([PropertyPath.Empty.Push("Zzz")]), static (_, _, c) =>
            {
                c.Enter("A");
                c.EnterKey("k");
                c.Changed("X", 1, 2);
                throw new InvalidOperationException();
            }));
        Assert.Equal(["Total", "Lines.Qty"], Paths(DiffCollector.Collect<string?>(null, null, null, null, ObjectWalk)));
        Assert.False(DiffCollector.HasDifferences<string?>(null, null, null, null, static (_, _, _) => { }));
    }

    [Fact]
    public void Collector_allocates_nothing_for_equal_positional_collections()
    {
        var ignores = new DiffSettings([PropertyPath.Empty.Push("Other"), PropertyPath.Empty.PushIndex(7).Push("X")]);
        foreach (var settings in new DiffSettings?[] { null, ignores })
        {
            var oldArray = ImmutableArray.Create(1, 2, 3);
            var newArray = ImmutableArray.Create(1, 2, 3);
            int[] oldInts = [1, 2, 3], newInts = [1, 2, 3];
            for (var i = 0; i < 3; i++) Measure(oldArray, newArray, oldInts, newInts, settings);
            Assert.Equal(0, Measure(oldArray, newArray, oldInts, newInts, settings));
        }
    }

    static long Measure(ImmutableArray<int> oldArray, ImmutableArray<int> newArray, int[] oldInts, int[] newInts, DiffSettings? settings)
    {
        var before = GC.GetAllocatedBytesForCurrentThread();
        var a = DiffCollector.Collect(oldArray, newArray, null, settings, ArrayWalk);
        var b = DiffCollector.HasDifferences(oldArray, newArray, null, settings, ArrayWalk);
        var c = DiffCollector.Collect(oldInts, newInts, null, settings, IntsWalk);
        var d = DiffCollector.HasDifferences(oldInts, newInts, null, settings, IntsWalk);
        var after = GC.GetAllocatedBytesForCurrentThread();
        Assert.True(a.IsEmpty && c.IsEmpty && !b && !d);
        return after - before;
    }

    static void ArrayWalk(ImmutableArray<int> o, ImmutableArray<int> n, DiffCollector c) => c.Array(o, n, IntWalk, false);

    static void IntsWalk(int[] o, int[] n, DiffCollector c) => c.Array(o, n, IntWalk, false);

    static void IntWalk(int o, int n, DiffCollector c)
    {
        if (o != n) c.Changed(o, n);
    }
    static string KeyOf(string value) => value[..1];

    static ImmutableArray<Difference> CompareNullable(string? oldValue, string? newValue, PropertyPath? path, DiffSettings? settings)
        => oldValue == newValue ? [] : [new(path ?? PropertyPath.Empty, oldValue, newValue)];

    static ImmutableArray<Difference> Compare(string oldValue, string newValue, PropertyPath? path, DiffSettings? settings)
        => oldValue == newValue ? [] : [new(path ?? PropertyPath.Empty, oldValue, newValue)];
}
