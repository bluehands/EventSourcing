using BenchmarkDotNet.Attributes;
using EventSourcing.Diff;

namespace EventSourcing.Diff.Benchmarks;

[MemoryDiagnoser]
public class DiffBenchmarks
{
    ScenarioData sameReference = null!;
    ScenarioData unchangedSharedCopy = null!;
    ScenarioData equalSeparateInstances = null!;
    ScenarioData oneDeepChange = null!;
    ScenarioData oneKeyedElementChange = null!;
    ScenarioData manyDictionaryChanges = null!;
    DiffSettings? settings;

    [Params(false, true)]
    public bool WithIgnoreList { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        sameReference = Scenarios.SameReference();
        unchangedSharedCopy = Scenarios.UnchangedSharedCopy();
        equalSeparateInstances = Scenarios.EqualSeparateInstances();
        oneDeepChange = Scenarios.OneDeepChange();
        oneKeyedElementChange = Scenarios.OneKeyedElementChange();
        manyDictionaryChanges = Scenarios.ManyDictionaryChanges();
        settings = Scenarios.Settings(WithIgnoreList);
    }

    [Benchmark] public object SameReference() => Run(sameReference);
    [Benchmark] public object UnchangedSharedCopy() => Run(unchangedSharedCopy);
    [Benchmark] public object EqualSeparateInstances() => Run(equalSeparateInstances);
    [Benchmark] public object OneDeepChange() => Run(oneDeepChange);
    [Benchmark] public object OneKeyedElementChange() => Run(oneKeyedElementChange);
    [Benchmark] public object ManyDictionaryChanges() => Run(manyDictionaryChanges);

    [Benchmark] public bool HasDifferencesSameReference() => Has(sameReference);
    [Benchmark] public bool HasDifferencesUnchangedSharedCopy() => Has(unchangedSharedCopy);
    [Benchmark] public bool HasDifferencesEqualSeparateInstances() => Has(equalSeparateInstances);
    [Benchmark] public bool HasDifferencesOneDeepChange() => Has(oneDeepChange);
    [Benchmark] public bool HasDifferencesOneKeyedElementChange() => Has(oneKeyedElementChange);
    [Benchmark] public bool HasDifferencesManyDictionaryChanges() => Has(manyDictionaryChanges);

    object Run(ScenarioData data) => Comparisons.Diff(data.Old, data.New, null, settings);

    bool Has(ScenarioData data) => Comparisons.HasDifferences(data.Old, data.New, settings);
}
