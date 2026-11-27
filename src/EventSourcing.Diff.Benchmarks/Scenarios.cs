using EventSourcing.Diff;

namespace EventSourcing.Diff.Benchmarks;

public sealed record ScenarioData(State Old, State New);

public static class Scenarios
{
    public static readonly PropertyPath IgnoredPath = PropertyPath.Empty.Push("Counter");

    public static DiffSettings? Settings(bool withIgnoreList) =>
        withIgnoreList ? new DiffSettings([IgnoredPath]) : null;

    public static ScenarioData SameReference()
    {
        var a = StateFactory.Create();
        return new(a, a);
    }

    public static ScenarioData UnchangedSharedCopy()
    {
        var a = StateFactory.Create();
        return new(a, a with { });
    }

    public static ScenarioData EqualSeparateInstances() => new(StateFactory.Create(), StateFactory.Create());

    public static ScenarioData OneDeepChange()
    {
        var a = StateFactory.Create();
        var address = a.Profile.Address with { Geo = a.Profile.Address.Geo with { Region = "Brandenburg" } };
        return new(a, a with { Profile = a.Profile with { Address = address } });
    }

    public static ScenarioData OneKeyedElementChange()
    {
        var a = StateFactory.Create();
        var lines = a.Lines.SetItem(25, a.Lines[25] with { Quantity = 999 });
        return new(a, a with { Lines = lines });
    }

    public static ScenarioData ManyDictionaryChanges()
    {
        var a = StateFactory.Create();
        var entries = a.Entries;
        for (var i = 0; i < 5; i++) entries = entries.SetItem($"key{i}", entries[$"key{i}"] with { Value = -i - 1 });
        for (var i = 5; i < 8; i++) entries = entries.Remove($"key{i}");
        for (var i = 0; i < 2; i++) entries = entries.Add($"new{i}", new Entry($"New {i}", i, Status.New));
        return new(a, a with { Entries = entries });
    }
}
