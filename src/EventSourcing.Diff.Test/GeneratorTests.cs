using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace EventSourcing.Diff.Test;

public class GeneratorTests
{
    const string Model = """
        using EventSourcing.Diff;
        using System.Collections.Generic;
        public record Address(string City);
        public record State(string Name, Address Address, Dictionary<string, Address> Entries, Address[] Items, int[] Numbers, int Version);
        [Diff(typeof(State), nameof(State.Version))]
        public static partial class Comparisons;
        public static class Scenario
        {
            public static object Run()
            {
                var before = new State("old", new("Paris"), new() { ["removed"] = new("A"), ["updated"] = new("B") }, [new("C")], [1], 1);
                var after = new State("new", new("Berlin"), new() { ["added"] = new("D"), ["updated"] = new("E") }, [new("F"), new("G")], [2], 2);
                return Comparisons.Diff(before, after);
            }
        }
        """;

    [Fact]
    public void Compares_nested_models_dictionaries_and_collections()
    {
        var differences = Run(Model);
        Assert.Equal(["Address.City", "Entries[removed]", "Entries[updated].City", "Entries[added]", "Items[0].City", "Items[1]", "Name", "Numbers"],
            differences.Select(d => d.PropertyPath.ToString()));
        Assert.Equal("Paris", differences[0].OldValue);
        Assert.Equal("Berlin", differences[0].NewValue);
    }

    [Fact]
    public void Regenerates_when_a_property_changes_on_the_same_driver()
    {
        var compilation = Compilation("""
            using EventSourcing.Diff;
            public record Model(int First);
            [Diff(typeof(Model))] public static partial class Comparisons;
            """);
        GeneratorDriver driver = Driver();
        driver = driver.RunGenerators(compilation, TestContext.Current.CancellationToken);
        Assert.Contains("oldValue.@First", driver.GetRunResult().GeneratedTrees.Single().ToString());
        var updated = compilation.ReplaceSyntaxTree(compilation.SyntaxTrees.Single(), CSharpSyntaxTree.ParseText("""
            using EventSourcing.Diff;
            public record Model(int Second);
            [Diff(typeof(Model))] public static partial class Comparisons;
            """, cancellationToken: TestContext.Current.CancellationToken));
        driver = driver.RunGenerators(updated, TestContext.Current.CancellationToken);
        var generated = driver.GetRunResult().GeneratedTrees.Single().ToString();
        Assert.Contains("oldValue.@Second", generated);
        Assert.DoesNotContain("oldValue.@First", generated);
    }

    [Fact]
    public void Hint_names_are_unique_across_namespaces()
    {
        var compilation = Generate("""
            using EventSourcing.Diff;
            namespace A { public record Model(int Value); [Diff(typeof(Model))] public static partial class Comparisons; }
            namespace B { public record Model(int Value); [Diff(typeof(Model))] public static partial class Comparisons; }
            """);
        Assert.Equal(2, compilation.SyntaxTrees.Count() - 1);
    }

    [Fact]
    public void Invalid_host_has_an_actionable_diagnostic()
    {
        Driver().RunGeneratorsAndUpdateCompilation(Compilation("""
            using EventSourcing.Diff;
            [Diff(typeof(string))] public class Comparisons;
            """), out _, out var diagnostics, TestContext.Current.CancellationToken);
        Assert.Contains(diagnostics, d => d.Id == "ESDIFF001");
    }

    [Fact]
    public void Record_hosts_are_rejected_instead_of_silently_skipped()
    {
        Driver().RunGeneratorsAndUpdateCompilation(Compilation("""
            using EventSourcing.Diff;
            [Diff(typeof(string))] public partial record Comparisons;
            """), out _, out var diagnostics, TestContext.Current.CancellationToken);
        Assert.Contains(diagnostics, d => d.Id == "ESDIFF001");
    }

    [Fact]
    public void Handles_null_and_shared_reference_models()
    {
        Assert.Empty(Run("""
            using EventSourcing.Diff;
            public record Model(string Name);
            [Diff(typeof(Model))] public static partial class Comparisons;
            public static class Scenario
            {
                public static object Run()
                {
                    var model = new Model("same");
                    return Comparisons.Diff(model, model).AddRange(Comparisons.Diff((Model?)null, null));
                }
            }
            """));
        var differences = Run("""
            using EventSourcing.Diff;
            public record Model(string Name);
            [Diff(typeof(Model))] public static partial class Comparisons;
            public static class Scenario { public static object Run() => Comparisons.Diff((Model?)null, new Model("new")); }
            """);
        Assert.Single(differences);
        Assert.Equal(PropertyPath.Empty, differences[0].PropertyPath);
        Assert.Null(differences[0].OldValue);
    }

    [Fact]
    public void Compares_concrete_base_and_most_specific_derived_instances()
    {
        var differences = Run("""
            using EventSourcing.Diff;
            public record Base(int Value);
            public record AChild(int Value, int ChildValue) : Base(Value);
            public record ZGrandchild(int Value, int ChildValue, int Extra) : AChild(Value, ChildValue);
            [Diff(typeof(Base))] public static partial class Comparisons;
            public static class Scenario
            {
                public static object Run() => Comparisons.Diff(new Base(1), new Base(2))
                    .AddRange(Comparisons.Diff((Base)new ZGrandchild(1, 2, 3), new ZGrandchild(1, 2, 4)))
                    .AddRange(Comparisons.Diff(new Base(1), new AChild(1, 2)));
            }
            """);
        Assert.Equal(["Value", "Extra", ""], differences.Select(d => d.PropertyPath.ToString()));
    }

    [Fact]
    public void Dispatches_interface_implementations_and_inherits_ignored_properties()
    {
        var differences = Run("""
            using EventSourcing.Diff;
            public interface IEntity { int Version { get; } }
            public record Entity(int Version, string Name) : IEntity;
            [Diff(typeof(IEntity), nameof(IEntity.Version))] public static partial class Comparisons;
            public static class Scenario { public static object Run() => Comparisons.Diff((IEntity)new Entity(1, "old"), new Entity(2, "new")); }
            """);
        Assert.Equal("Name", Assert.Single(differences).PropertyPath.ToString());
    }

    [Fact]
    public void Atomic_interface_configuration_preserves_value_granularity()
    {
        var differences = Run("""
            using EventSourcing.Diff;
            public interface IId { int Value { get; } }
            public record Id(int Value) : IId;
            public record Model(Id Id);
            [Diff(typeof(Model)), Diff(typeof(IId), TreatAsValue = true)] public static partial class Comparisons;
            public static class Scenario { public static object Run() => Comparisons.Diff(new Model(new(1)), new Model(new(2))); }
            """);
        Assert.Equal("Id", Assert.Single(differences).PropertyPath.ToString());
    }

    [Fact]
    public void Custom_comparisons_in_other_partial_declarations_are_used_for_collection_elements()
    {
        Assert.Empty(Run("""
            using EventSourcing.Diff;
            using System;
            using System.Collections.Immutable;
            public record Model(string[] Names);
            [Diff(typeof(Model))] public static partial class Comparisons;
            public static partial class Comparisons
            {
                public static ImmutableArray<Difference> Diff(string? before, string? after, PropertyPath? path, DiffSettings? settings)
                    => StringComparer.OrdinalIgnoreCase.Equals(before, after) ? [] : [new(path ?? PropertyPath.Empty, before, after)];
            }
            public static class Scenario { public static object Run() => Comparisons.Diff(new Model(["alice"]), new Model(["ALICE"])); }
            """));
    }

    [Fact]
    public void Ignores_inaccessible_getters_and_indexers_and_escapes_identifiers()
    {
        var differences = Run("""
            using EventSourcing.Diff;
            public class Model
            {
                public string @event { get; init; } = "";
                public int Hidden { private get; set; }
                public int this[int index] => index;
                public static int Static => 1;
            }
            [Diff(typeof(Model))] public static partial class Comparisons;
            public static class Scenario { public static object Run() => Comparisons.Diff(new Model { @event = "a" }, new Model { @event = "b" }); }
            """);
        Assert.Equal("event", Assert.Single(differences).PropertyPath.ToString());
    }

    [Fact]
    public void Uses_semantic_attribute_arguments_and_ignores_nested_paths()
    {
        Assert.Empty(Run("""
            using EventSourcing.Diff;
            public record Model(int Value, Model? Child);
            public static class Names { public const string Ignored = nameof(Model.Value); }
            [Diff(typeToDiff: typeof(Model), propertiesToIgnore: new[] { Names.Ignored })] public static partial class Comparisons;
            public static class Scenario
            {
                public static object Run() => Comparisons.Diff(new Model(1, new(2, null)), new Model(3, null),
                    settings: new DiffSettings([PropertyPath.Empty.Push("Child")]));
            }
            """));
    }

    [Fact]
    public void Compares_immutable_collections_including_default_arrays()
    {
        var differences = Run("""
            using EventSourcing.Diff;
            using System.Collections.Immutable;
            public record Item(string Name);
            public record Model(ImmutableArray<Item> Items, ImmutableArray<int> Numbers, ImmutableDictionary<string, Item> Entries);
            [Diff(typeof(Model))] public static partial class Comparisons;
            public static class Scenario
            {
                public static object Run() => Comparisons.Diff(
                    new Model(default, [1], ImmutableDictionary<string, Item>.Empty.Add("key", new("old"))),
                    new Model([], [2], ImmutableDictionary<string, Item>.Empty.Add("key", new("new"))));
            }
            """);
        Assert.Equal(["Entries[key].Name", "Items", "Numbers"], differences.Select(d => d.PropertyPath.ToString()));
    }

    [Fact]
    public void Closed_generic_nullable_and_value_types_compile()
    {
        var differences = Run("""
            using EventSourcing.Diff;
            using System;
            using System.Collections.Generic;
            public record Box<T>(T Value);
            public record Model(Box<int?> Number, IReadOnlyDictionary<string, Box<string?>?> Values, Guid Id);
            [Diff(typeof(Model))] public static partial class Comparisons;
            public static class Scenario
            {
                public static object Run() => Comparisons.Diff(new Model(new(1), new Dictionary<string, Box<string?>?> { ["key"] = null }, Guid.Empty),
                    new Model(new(2), new Dictionary<string, Box<string?>?> { ["key"] = new(null) }, Guid.Empty));
            }
            """);
        Assert.Equal(["Number.Value", "Values[key]"], differences.Select(d => d.PropertyPath.ToString()));
    }

    [Fact]
    public void File_local_hosts_have_an_actionable_diagnostic()
    {
        Driver().RunGeneratorsAndUpdateCompilation(Compilation("""
            using EventSourcing.Diff;
            [Diff(typeof(string))] file static partial class Comparisons;
            """), out _, out var diagnostics, TestContext.Current.CancellationToken);
        Assert.Contains(diagnostics, d => d.Id == "ESDIFF001");
    }

    [Theory]
    [InlineData("public record Model<T>(T Value);", "typeof(Model<>)", "ESDIFF002")]
    [InlineData("public ref struct Model { }", "typeof(Model)", "ESDIFF002")]
    [InlineData("public record Model(int Value);", "typeof(Model)", "ESDIFF003")]
    public void Unsupported_models_and_invalid_custom_methods_have_diagnostics(string model, string type, string expected)
    {
        var custom = expected == "ESDIFF003" ? "public static int Diff(Model oldValue, Model newValue) => 0;" : "";
        Driver().RunGeneratorsAndUpdateCompilation(Compilation($$"""
            using EventSourcing.Diff;
            {{model}}
            [Diff({{type}})] public static partial class Comparisons { {{custom}} }
            """), out _, out var diagnostics, TestContext.Current.CancellationToken);
        Assert.Contains(diagnostics, d => d.Id == expected);
    }

    internal static ImmutableArray<Difference> Run(string source)
    {
        var compilation = Generate(source);
        using var assembly = new MemoryStream();
        var result = compilation.Emit(assembly, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        var loaded = Assembly.Load(assembly.ToArray());
        return (ImmutableArray<Difference>)loaded.GetType("Scenario")!.GetMethod("Run")!.Invoke(null, null)!;
    }

    static CSharpCompilation Generate(string source)
    {
        var driver = Driver().RunGeneratorsAndUpdateCompilation(Compilation(source), out var output, out var diagnostics, TestContext.Current.CancellationToken);
        Assert.DoesNotContain(driver.GetRunResult().Results, r => r.Exception is not null);
        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.True(!output.GetDiagnostics().Any(d => d.Severity == DiagnosticSeverity.Error),
            string.Join(Environment.NewLine, output.GetDiagnostics()) + Environment.NewLine +
            string.Join(Environment.NewLine, driver.GetRunResult().GeneratedTrees.Select(t => t.ToString())));
        return (CSharpCompilation)output;
    }

    static GeneratorDriver Driver() => CSharpGeneratorDriver.Create(new SourceGenerator.DiffGenerator().AsSourceGenerator());

    static CSharpCompilation Compilation(string source)
    {
        var paths = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Append(typeof(Difference).Assembly.Location).Distinct(StringComparer.OrdinalIgnoreCase);
        return CSharpCompilation.Create("DiffConsumer_" + Guid.NewGuid().ToString("N"),
            [CSharpSyntaxTree.ParseText(source, cancellationToken: TestContext.Current.CancellationToken)], paths.Select(p => MetadataReference.CreateFromFile(p)),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
    }
}
