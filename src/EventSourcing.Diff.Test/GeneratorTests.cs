using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.CompilerServices;
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

    const string Lines = """
        using EventSourcing.Diff;
        using System.Collections.Generic;
        using System.Collections.Immutable;
        public record Line(int Id, int Quantity);
        """;

    static string[] Describe(ImmutableArray<Difference> differences) => differences.Select(d => $"{d.Kind} {d.PropertyPath}").ToArray();

    static ImmutableArray<Diagnostic> Diagnose(string source, out Compilation output)
    {
        Driver().RunGeneratorsAndUpdateCompilation(Compilation(source), out output, out var diagnostics, TestContext.Current.CancellationToken);
        return diagnostics;
    }

    [Fact]
    public void Framework_types_are_compared_by_equality_without_walking_their_members()
    {
        const string source = """
            using EventSourcing.Diff;
            using System;
            using System.Globalization;
            using System.Text;
            using System.Text.RegularExpressions;
            public record Model(Encoding Encoding, Exception Error, CultureInfo Culture, Regex Pattern);
            [Diff(typeof(Model))] public static partial class Comparisons;
            public static class Scenario
            {
                public static object Run()
                {
                    var error = new Exception("x");
                    var pattern = new Regex("a");
                    var before = new Model(Encoding.UTF8, error, CultureInfo.InvariantCulture, pattern);
                    return Comparisons.Diff(before, before with { }).AddRange(Comparisons.Diff(before, new Model(Encoding.UTF8, error, CultureInfo.InvariantCulture, pattern)))
                        .AddRange(Comparisons.Diff(before, before with { Encoding = Encoding.ASCII }));
                }
            }
            """;
        Assert.Equal(["Changed Encoding"], Describe(Run(source)));
        Assert.DoesNotContain("System.Reflection", string.Concat(Generate(source).SyntaxTrees.Skip(1).Select(t => t.ToString())));
    }

    [Fact]
    public void Sets_ignore_order_and_report_membership()
    {
        var differences = Run($$"""
            {{Lines}}
            public record Model(HashSet<string> Tags, ImmutableHashSet<string> Frozen, ISet<int> Numbers);
            [Diff(typeof(Model))] public static partial class Comparisons;
            public static class Scenario
            {
                public static object Run() => Comparisons.Diff(
                    new Model(["a", "b"], ["x"], new SortedSet<int> { 1 }), new Model(["b", "c"], ["x"], new SortedSet<int> { 1 }));
            }
            """);
        Assert.Equal(["Removed Tags", "Added Tags"], Describe(differences));
        Assert.Equal(["a", "c"], [differences[0].OldValue, differences[1].NewValue]);
        Assert.Empty(Run($$"""
            {{Lines}}
            public record Model(HashSet<string> Tags, ImmutableHashSet<string> Frozen);
            [Diff(typeof(Model))] public static partial class Comparisons;
            public static class Scenario
            {
                public static object Run() => Comparisons.Diff(new Model(["a", "b"], ImmutableHashSet.Create("a", "b")), new Model(["b", "a"], ImmutableHashSet.Create("b", "a")));
            }
            """));
    }

    [Fact]
    public void Idictionary_properties_are_compared_by_key()
    {
        var differences = Run("""
            using EventSourcing.Diff;
            using System.Collections.Generic;
            public record Model(IDictionary<string, int> Map);
            [Diff(typeof(Model))] public static partial class Comparisons;
            public static class Scenario
            {
                public static object Run() => Comparisons.Diff(new Model(new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 }), new Model(new Dictionary<string, int> { ["b"] = 2, ["a"] = 1 }))
                    .AddRange(Comparisons.Diff(new Model(new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 }), new Model(new Dictionary<string, int> { ["b"] = 3, ["c"] = 1 })));
            }
            """);
        Assert.Equal(["Removed Map[a]", "Changed Map[b]", "Added Map[c]"], Describe(differences));
    }

    [Fact]
    public void Nullable_immutable_arrays_are_compared_by_content()
    {
        var differences = Run("""
            using EventSourcing.Diff;
            using System.Collections.Immutable;
            public record Model(ImmutableArray<int>? Numbers);
            [Diff(typeof(Model))] public static partial class Comparisons;
            public static class Scenario
            {
                public static object Run() => Comparisons.Diff(new Model(ImmutableArray.Create(1)), new Model(ImmutableArray.Create(1)))
                    .AddRange(Comparisons.Diff(new Model(null), new Model(null)))
                    .AddRange(Comparisons.Diff(new Model(null), new Model(ImmutableArray.Create(1))))
                    .AddRange(Comparisons.Diff(new Model(ImmutableArray.Create(1)), new Model(ImmutableArray.Create(2))));
            }
            """);
        Assert.Equal(["Changed Numbers", "Changed Numbers"], Describe(differences));
    }

    [Fact]
    public void MatchBy_matches_elements_by_key_ignoring_order()
    {
        var differences = Run($$"""
            {{Lines}}
            public record Order(List<Line> Lines);
            [Diff(typeof(Order)), Diff(typeof(Line), MatchBy = nameof(Line.Id))] public static partial class Comparisons;
            public static class Scenario
            {
                public static object Run() => Comparisons.Diff(new Order([new(2, 1), new(1, 1)]), new Order([new(1, 1), new(2, 1)]))
                    .AddRange(Comparisons.Diff(new Order([new(1, 1), new(2, 1)]), new Order([new(3, 1), new(2, 5)])));
            }
            """);
        Assert.Equal(["Removed Lines[1]", "Changed Lines[2].Quantity", "Added Lines[3]"], Describe(differences));
    }

    [Fact]
    public void MatchBy_declared_on_a_base_interface_applies_to_polymorphic_lists()
    {
        var differences = Run("""
            using EventSourcing.Diff;
            using System.Collections.Generic;
            public interface IHasId { int Id { get; } }
            public record A(int Id, string Name) : IHasId;
            public record B(int Id, int Size) : IHasId;
            public record Model(IReadOnlyList<IHasId> Items);
            [Diff(typeof(Model)), Diff(typeof(IHasId), MatchBy = nameof(IHasId.Id))] public static partial class Comparisons;
            public static class Scenario
            {
                public static object Run() => Comparisons.Diff(new Model([new A(1, "x"), new B(2, 3)]), new Model([new B(2, 4), new A(1, "x")]));
            }
            """);
        Assert.Equal(["Changed Items[2].Size"], Describe(differences));
    }

    [Fact]
    public void MatchBy_uses_custom_element_comparisons_for_matched_pairs()
    {
        var differences = Run("""
            using EventSourcing.Diff;
            using System.Collections.Generic;
            using System.Collections.Immutable;
            public record Line(int Id, int Quantity);
            public record Order(List<Line> Lines);
            [Diff(typeof(Order)), Diff(typeof(Line), MatchBy = nameof(Line.Id))]
            public static partial class Comparisons
            {
                public static ImmutableArray<Difference> Diff(Line? a, Line? b, PropertyPath? path, DiffSettings? settings)
                    => a?.Quantity == b?.Quantity ? [] : [new(path ?? PropertyPath.Empty, a, b)];
            }
            public static class Scenario { public static object Run() => Comparisons.Diff(new Order([new(1, 1)]), new Order([new(1, 2)])); }
            """);
        Assert.Equal(["Changed Lines[1]"], Describe(differences));
    }

    [Fact]
    public void MatchBy_supports_immutable_arrays_and_sets()
    {
        var differences = Run($$"""
            {{Lines}}
            public record Model(ImmutableArray<Line> Lines, HashSet<Line> Set);
            [Diff(typeof(Model)), Diff(typeof(Line), MatchBy = nameof(Line.Id))] public static partial class Comparisons;
            public static class Scenario
            {
                public static object Run() => Comparisons.Diff(new Model(default, [new(1, 1)]), new Model([new(1, 1)], [new(1, 2)]));
            }
            """);
        Assert.Equal(["Changed Lines", "Changed Set[1].Quantity"], Describe(differences));
    }

    [Fact]
    public void MatchBy_supports_nullable_value_and_reference_keys()
    {
        const string model = """
            using EventSourcing.Diff;
            using System.Collections.Generic;
            public record Row(int? Id, string? Code, int Value);
            public record Model(List<Row> ById, List<Row> ByCode);
            [Diff(typeof(Model))] public static partial class Comparisons;
            """;
        Assert.Equal(["Changed ById[1].Value"], Describe(Run(model.Replace("[Diff(typeof(Model))]", "[Diff(typeof(Model)), Diff(typeof(Row), MatchBy = nameof(Row.Id))]") + """
            public static class Scenario { public static object Run() => Comparisons.Diff(new Model([new(1, "a", 1)], []), new Model([new(1, "a", 2)], [])); }
            """)));
        Assert.Equal(["Changed ByCode[a].Value"], Describe(Run(model.Replace("[Diff(typeof(Model))]", "[Diff(typeof(Model)), Diff(typeof(Row), MatchBy = nameof(Row.Code))]") + """
            public static class Scenario { public static object Run() => Comparisons.Diff(new Model([], [new(1, "a", 1)]), new Model([], [new(1, "a", 2)])); }
            """)));
        var exception = Assert.Throws<System.Reflection.TargetInvocationException>(() => Run(model.Replace("[Diff(typeof(Model))]", "[Diff(typeof(Model)), Diff(typeof(Row), MatchBy = nameof(Row.Id))]") + """
            public static class Scenario { public static object Run() => Comparisons.Diff(new Model([new(null, "a", 1)], []), new Model([], [])); }
            """));
        Assert.IsType<InvalidOperationException>(exception.InnerException);
        Assert.Contains("ById", exception.InnerException.Message);
    }

    [Fact]
    public void MatchBy_works_with_explicit_interface_implementations()
    {
        var differences = Run("""
            using EventSourcing.Diff;
            using System.Collections.Generic;
            public interface IEntity { int Id { get; } }
            public record Item(int Value) : IEntity { int IEntity.Id => Value / 10; public int Size { get; init; } }
            public record Model(List<Item> Items);
            [Diff(typeof(Model)), Diff(typeof(IEntity), MatchBy = nameof(IEntity.Id))] public static partial class Comparisons;
            public static class Scenario { public static object Run() => Comparisons.Diff(new Model([new(10) { Size = 1 }]), new Model([new(10) { Size = 2 }])); }
            """);
        Assert.Equal(["Changed Items[1].Size"], Describe(differences));
    }

    [Theory]
    [InlineData("Missing")]
    [InlineData("Tags")]
    [InlineData("Secret")]
    [InlineData("Item")]
    public void Invalid_MatchBy_properties_are_rejected(string matchBy)
    {
        var diagnostics = Diagnose($$"""
            using EventSourcing.Diff;
            using System.Collections.Generic;
            public class Entry { public List<int> Tags { get; } = []; public int Secret { private get; set; } public int this[int i] => i; }
            [Diff(typeof(Entry), MatchBy = "{{matchBy}}")] public static partial class Comparisons;
            """, out _);
        Assert.Contains(diagnostics, d => d.Id == "ESDIFF006" && d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void Conflicting_MatchBy_values_are_rejected()
    {
        var diagnostics = Diagnose($$"""
            {{Lines}}
            [Diff(typeof(Line), MatchBy = "Id"), Diff(typeof(Line), MatchBy = "Quantity")] public static partial class Comparisons;
            """, out _);
        Assert.Contains(diagnostics, d => d.Id == "ESDIFF007");
    }

    [Fact]
    public void Ambiguous_interface_properties_are_reported_and_can_be_ignored_or_redeclared()
    {
        const string model = """
            using EventSourcing.Diff;
            public interface IA { int X { get; } }
            public interface IB { string X { get; } }
            """;
        var diagnostics = Diagnose(model + "public interface IC : IA, IB { } [Diff(typeof(IC))] public static partial class Comparisons;", out var output);
        Assert.Contains(diagnostics, d => d.Id == "ESDIFF004");
        Assert.DoesNotContain(output.GetDiagnostics(TestContext.Current.CancellationToken), d => d.Id == "CS0229");
        Generate(model + "public interface IC : IA, IB { } [Diff(typeof(IC), nameof(IA.X))] public static partial class Comparisons;");
        Generate(model + "public interface IC : IA, IB { new int X { get; } } [Diff(typeof(IC))] public static partial class Comparisons;");
    }

    [Fact]
    public void Unsupported_types_report_the_path_and_the_attribute()
    {
        const string source = """
            using EventSourcing.Diff;
            public record Settings(dynamic Value);
            public record Customer(Settings Settings);
            [Diff(typeof(Customer))] public static partial class Comparisons;
            """;
        var diagnostic = Assert.Single(Diagnose(source, out _), d => d.Id == "ESDIFF002");
        Assert.Contains("reached via 'Customer.Settings.Value'", diagnostic.GetMessage());
        Assert.Contains("TreatAsValue", diagnostic.GetMessage());
        Assert.Equal("Diff(typeof(Customer))", source.Substring(diagnostic.Location.SourceSpan.Start, diagnostic.Location.SourceSpan.Length));
    }

    [Fact]
    public void Unknown_ignored_properties_produce_a_warning()
    {
        var diagnostics = Diagnose("""
            using EventSourcing.Diff;
            public record Model(int Name);
            [Diff(typeof(Model), "Nmae")] public static partial class Comparisons;
            """, out _);
        Assert.Contains(diagnostics, d => d.Id == "ESDIFF005" && d.Severity == DiagnosticSeverity.Warning);
    }

    [Fact]
    public void Keyword_namespaces_are_escaped()
    {
        Assert.Equal(["Changed V"], Describe(Run("""
            using EventSourcing.Diff;
            namespace @event { public record Model(int V); [Diff(typeof(Model))] public static partial class Comparisons; }
            public static class Scenario { public static object Run() => @event.Comparisons.Diff(new @event.Model(1), new @event.Model(2)); }
            """)));
    }

    [Fact]
    public void Hint_names_are_readable_and_distinct_for_case_variants()
    {
        var result = Driver().RunGenerators(Compilation("""
            using EventSourcing.Diff;
            namespace A
            {
                public record Model(int V);
                [Diff(typeof(Model))] public static partial class Comparisons;
                [Diff(typeof(Model))] public static partial class comparisons;
            }
            """), TestContext.Current.CancellationToken).GetRunResult();
        var names = result.Results.Single().GeneratedSources.Select(s => s.HintName).ToArray();
        Assert.Equal(2, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Single(names, n => n.StartsWith("A.Comparisons.", StringComparison.Ordinal) && n.EndsWith(".Diff.g.cs", StringComparison.Ordinal));
        Assert.Single(names, n => n.StartsWith("A.comparisons.", StringComparison.Ordinal));
    }

    const string RichModel = """
        using EventSourcing.Diff;
        using System;
        using System.Collections.Generic;
        using System.Collections.Immutable;
        public enum Kind { A, B }
        public record Leaf(string Name, int Value);
        public record Line(int Id, int Quantity);
        public record Money(decimal Amount, string Currency);
        public readonly record struct Stamp(int Ticks, string Zone);
        public abstract record Shape(int Id);
        public record Circle(int Id, int Radius) : Shape(Id);
        public record Square(int Id, int Side) : Shape(Id);
        public record Rich(int Version, string? Name, Kind Kind, Leaf? Leaf, Shape? Shape, int? Count, Money Price, Stamp? Last,
            ImmutableArray<Leaf> Items, Leaf[] Array, List<Leaf> List, IEnumerable<int> Numbers, ImmutableDictionary<string, Leaf> Map,
            Dictionary<string, int> Counts, IDictionary<string, Leaf> Open, HashSet<string> Tags, ImmutableArray<Line> Lines);
        [Diff(typeof(Rich), nameof(Rich.Version)), Diff(typeof(Line), MatchBy = nameof(Line.Id))]
        public static partial class Comparisons
        {
            public static ImmutableArray<Difference> Diff(Money? a, Money? b, PropertyPath? path, DiffSettings? settings)
                => a?.Amount == b?.Amount ? [] : [new(path ?? PropertyPath.Empty, a, b)];
            public static ImmutableArray<Difference> Diff(Stamp a, Stamp b, PropertyPath? path, DiffSettings? settings)
                => a.Ticks == b.Ticks ? [] : [new(path ?? PropertyPath.Empty, a, b)];
        }
        public static class Scenario
        {
            static Rich Make() => new(1, "n", Kind.A, new("l", 1), new Circle(1, 1), 1, new(1m, "EUR"), new Stamp(1, "a"),
                [new("a", 1), new("b", 2)], [new("a", 1)], [new("a", 1)], new[] { 1, 2 }, ImmutableDictionary<string, Leaf>.Empty.Add("k", new("a", 1)),
                new() { ["k"] = 1 }, new Dictionary<string, Leaf> { ["k"] = new("a", 1) }, ["x", "y"], [new(1, 1), new(2, 2)]);

            public static object Run()
            {
                var b = Make();
                var values = new List<Rich?>
                {
                    null, b, Make(), b with { Version = 2 }, b with { Name = null }, b with { Name = "other" }, b with { Kind = Kind.B },
                    b with { Leaf = null }, b with { Leaf = new("l", 2) }, b with { Shape = null }, b with { Shape = new Circle(1, 2) },
                    b with { Shape = new Square(1, 1) }, b with { Shape = new Circle(1, 1) }, b with { Count = null }, b with { Count = 2 },
                    b with { Price = new(1m, "USD") }, b with { Price = new(2m, "EUR") }, b with { Last = null }, b with { Last = new Stamp(1, "b") },
                    b with { Last = new Stamp(2, "a") }, b with { Items = default }, b with { Items = [] }, b with { Items = [new("a", 1)] },
                    b with { Items = [new("a", 1), new("b", 3)] }, b with { Array = [] }, b with { Array = [new("a", 2)] },
                    b with { List = [new("a", 1), new("z", 1)] }, b with { Numbers = new[] { 1, 3 } }, b with { Numbers = new List<int> { 1, 2 } },
                    b with { Map = ImmutableDictionary<string, Leaf>.Empty }, b with { Map = b.Map.SetItem("k", new("a", 9)) },
                    b with { Map = b.Map.Add("j", new("a", 1)) }, b with { Counts = new() { ["k"] = 2 } }, b with { Counts = new() },
                    b with { Open = new Dictionary<string, Leaf>() }, b with { Open = new Dictionary<string, Leaf> { ["k"] = new("a", 5) } },
                    b with { Tags = ["x"] }, b with { Tags = ["y", "x"] }, b with { Lines = [new(2, 2), new(1, 1)] },
                    b with { Lines = [new(1, 1), new(2, 3)] }, b with { Lines = default }, b with { Lines = [] },
                };
                var P = PropertyPath.Empty;
                var settings = new DiffSettings?[]
                {
                    null, new DiffSettings(), new DiffSettings([P.Push("Leaf")]), new DiffSettings([P.Push("Items").PushIndex(1)]),
                    new DiffSettings([P.Push("Map").PushKey("k")]), new DiffSettings([P.Push("Shape").Push("Radius")]),
                    new DiffSettings([P.Push("Lines").PushKey(1)]), new DiffSettings([P.Push("Price"), P.Push("Last")]),
                    new DiffSettings([P.Push("Name"), P.Push("Items"), P.Push("Open").PushKey("k"), P.Push("Tags"), P.Push("Counts")]),
                };
                int total = 0, different = 0;
                var mismatches = new List<string>();
                for (var s = 0; s < settings.Length; s++)
                    for (var i = 0; i < values.Count; i++)
                        for (var j = 0; j < values.Count; j++)
                        {
                            var diff = Comparisons.Diff(values[i], values[j], null, settings[s]);
                            var has = Comparisons.HasDifferences(values[i], values[j], settings[s]);
                            if (has != values[i].HasDifferences(values[j], settings[s]) || has == diff.IsEmpty) mismatches.Add($"{s}:{i}:{j}");
                            total++;
                            if (!diff.IsEmpty) different++;
                        }
                return $"{string.Join(",", mismatches)}|{total}|{different}";
            }
        }
        """;

    [Fact]
    public void HasDifferences_matches_Diff_for_a_broad_model_over_many_pairs_and_ignore_lists()
    {
        var parts = ((string)Invoke(RichModel)).Split('|');
        Assert.Equal("", parts[0]);
        var total = int.Parse(parts[1]);
        var different = int.Parse(parts[2]);
        Assert.True(total > 10000);
        Assert.InRange(different, total / 4, total - 1000);
    }

    [Fact]
    public void HasDifferences_stops_at_the_first_difference()
    {
        Assert.Equal("True0|True1|2", Invoke("""
            using EventSourcing.Diff;
            using System.Collections.Immutable;
            public record Tag(string Value);
            public record Model(int First, Tag Second, Tag Third);
            [Diff(typeof(Model))]
            public static partial class Comparisons
            {
                public static int Calls;
                public static ImmutableArray<Difference> Diff(Tag? a, Tag? b, PropertyPath? path, DiffSettings? settings)
                {
                    Calls++;
                    return a?.Value == b?.Value ? [] : [new(path ?? PropertyPath.Empty, a, b)];
                }
            }
            public static class Scenario
            {
                public static object Run()
                {
                    var a = new Model(1, new("a"), new("a"));
                    Comparisons.Calls = 0;
                    var first = Comparisons.HasDifferences(a, a with { First = 2 });
                    var firstCalls = Comparisons.Calls;
                    Comparisons.Calls = 0;
                    var second = Comparisons.HasDifferences(a, a with { Second = new("b"), Third = new("b") });
                    var secondCalls = Comparisons.Calls;
                    Comparisons.Calls = 0;
                    Comparisons.Diff(a, a with { Second = new("b"), Third = new("b") });
                    return $"{first}{firstCalls}|{second}{secondCalls}|{Comparisons.Calls}";
                }
            }
            """));
    }

    [Fact]
    public void HasDifferences_honors_ignored_paths()
    {
        Assert.Equal("False,False,True,True,True", Invoke("""
            using EventSourcing.Diff;
            public record Leaf(int Value);
            public record Model(int A, Leaf B, Leaf[] C);
            [Diff(typeof(Model))] public static partial class Comparisons;
            public static class Scenario
            {
                public static object Run()
                {
                    var settings = new DiffSettings([PropertyPath.Empty.Push("B").Push("Value"), PropertyPath.Empty.Push("C").PushIndex(1)]);
                    var a = new Model(1, new(1), [new(1), new(1)]);
                    return string.Join(",", new[]
                    {
                        Comparisons.HasDifferences(a, a with { B = new(2) }, settings),
                        Comparisons.HasDifferences(a, a with { C = [new(1), new(5)] }, settings),
                        Comparisons.HasDifferences(a, a with { C = [new(2), new(1)] }, settings),
                        Comparisons.HasDifferences(a, a with { A = 2 }, settings),
                        Comparisons.HasDifferences(a, a with { B = new(2) }),
                    });
                }
            }
            """));
    }

    [Fact]
    public void HasDifferences_handles_null_and_runtime_type_transitions()
    {
        Assert.Equal("False,True,True,False,True,True", Invoke("""
            using EventSourcing.Diff;
            public abstract record Shape(int Id);
            public record Circle(int Id, int Radius) : Shape(Id);
            public record Square(int Id, int Side) : Shape(Id);
            [Diff(typeof(Shape))] public static partial class Comparisons;
            public static class Scenario
            {
                public static object Run()
                {
                    Shape? none = null;
                    Shape circle = new Circle(1, 1);
                    return string.Join(",", new[]
                    {
                        Comparisons.HasDifferences(none, none), Comparisons.HasDifferences(none, circle), Comparisons.HasDifferences(circle, none),
                        Comparisons.HasDifferences(circle, new Circle(1, 1)), Comparisons.HasDifferences(circle, new Circle(1, 2)),
                        Comparisons.HasDifferences(circle, new Square(1, 1)),
                    });
                }
            }
            """));
    }

    [Fact]
    public void HasDifferences_handles_keyed_set_and_dictionary_properties()
    {
        Assert.Equal("False,True,True,True,True,True,True", Invoke($$"""
            {{Lines}}
            public record Model(ImmutableArray<Line> Lines, HashSet<string> Tags, Dictionary<string, int> Counts, ImmutableDictionary<string, int> Frozen);
            [Diff(typeof(Model)), Diff(typeof(Line), MatchBy = nameof(Line.Id))] public static partial class Comparisons;
            public static class Scenario
            {
                static Model Make() => new([new(1, 1), new(2, 2)], ["x", "y"], new() { ["k"] = 1 }, ImmutableDictionary<string, int>.Empty.Add("k", 1));
                public static object Run()
                {
                    var a = Make();
                    return string.Join(",", new[]
                    {
                        Comparisons.HasDifferences(a, new Model([new(2, 2), new(1, 1)], ["y", "x"], new() { ["k"] = 1 }, ImmutableDictionary<string, int>.Empty.Add("k", 1))),
                        Comparisons.HasDifferences(a, a with { Lines = [new(1, 1), new(2, 3)] }),
                        Comparisons.HasDifferences(a, a with { Lines = [new(1, 1)] }),
                        Comparisons.HasDifferences(a, a with { Tags = ["x"] }),
                        Comparisons.HasDifferences(a, a with { Counts = new() { ["k"] = 2 } }),
                        Comparisons.HasDifferences(a, a with { Counts = new() { ["k"] = 1, ["j"] = 1 } }),
                        Comparisons.HasDifferences(a, a with { Frozen = ImmutableDictionary<string, int>.Empty }),
                    });
                }
            }
            """));
    }

    const string ExtensionModel = """
        using EventSourcing.Diff;
        public record Leaf(string Name);
        public readonly record struct Point(int X);
        public abstract record Shape(int Id);
        public record Circle(int Id, int Radius) : Shape(Id);
        public record Holder(Point? At, Leaf Leaf, string Text);
        [Diff(typeof(Holder)), Diff(typeof(Shape)), Diff(typeof(Point))] public static partial class Comparisons;
        public static class Scenario
        {
            public static object Run()
            {
                Shape a = new Circle(1, 1);
                Shape b = new Circle(1, 2);
                var first = new Holder(new Point(1), new("a"), "t");
                var second = first with { At = new Point(2) };
                return $"{a.Diff(b).Length}{a.HasDifferences(b)}{first.Diff(second).Length}{first.HasDifferences(second)}{new Point(1).HasDifferences(new Point(2))}{new Point(1).Diff(new Point(1)).Length}";
            }
        }
        """;

    [Fact]
    public void Configured_types_get_extension_methods_and_property_only_types_stay_static()
    {
        Assert.Equal("1True1TrueTrue0", Invoke(ExtensionModel));
        var methods = Load(ExtensionModel).GetType("Comparisons")!.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.Name is "Diff" or "HasDifferences").ToArray();
        static bool IsExtension(MethodInfo method) => method.IsDefined(typeof(ExtensionAttribute), false);
        Assert.Equal(["Holder", "Point", "Shape"],
            methods.Where(IsExtension).Select(m => m.GetParameters()[0].ParameterType.Name).Distinct().OrderBy(n => n, StringComparer.Ordinal));
        foreach (var plain in new[] { "Leaf", "String", "Nullable`1" })
        {
            var matching = methods.Where(m => m.GetParameters()[0].ParameterType.Name == plain).ToArray();
            Assert.NotEmpty(matching);
            Assert.DoesNotContain(matching, IsExtension);
        }
    }

    [Fact]
    public void Unchanged_states_are_compared_without_allocations()
    {
        var deltas = (long[])Invoke("""
            using EventSourcing.Diff;
            using System;
            using System.Collections.Generic;
            using System.Collections.Immutable;
            public enum Kind { A, B }
            public record Leaf(string Name, int Value);
            public abstract record Shape(int Id);
            public record Circle(int Id, int Radius) : Shape(Id);
            public record Square(int Id, int Side) : Shape(Id);
            public record State(int Version, string Name, Guid Id, DateTimeOffset At, Kind Kind, Leaf Leaf, Shape Shape,
                ImmutableArray<Leaf> Items, Leaf[] Array, List<Leaf> List, ImmutableDictionary<string, Leaf> Map);
            [Diff(typeof(State))] public static partial class Comparisons;
            public static class Scenario
            {
                static State Build() => new(1, "name", Guid.Parse("11111111-1111-1111-1111-111111111111"), new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                    Kind.B, new("leaf", 1), new Circle(1, 2), [new("a", 1), new("b", 2)], [new("c", 3)], [new("d", 4)],
                    ImmutableDictionary<string, Leaf>.Empty.Add("k", new("e", 5)).Add("j", new("f", 6)));

                public static object Run()
                {
                    var P = PropertyPath.Empty;
                    var a = Build();
                    var pairs = new[] { (a, a with { }), (a, Build()) };
                    var settings = new DiffSettings?[] { null, new DiffSettings([P.Push("Name"), P.Push("Items").PushIndex(0), P.Push("Map").PushKey("k"), P.Push("Shape").Push("Radius")]) };
                    var results = new long[pairs.Length * settings.Length * 2];
                    var slot = 0;
                    foreach (var (x, y) in pairs)
                        foreach (var s in settings)
                        {
                            for (var warm = 0; warm < 5; warm++)
                            {
                                Comparisons.HasDifferences(x, y, s);
                                Comparisons.Diff(x, y, null, s);
                            }
                            var found = false;
                            var before = GC.GetAllocatedBytesForCurrentThread();
                            for (var n = 0; n < 100; n++) found |= Comparisons.HasDifferences(x, y, s);
                            results[slot++] = found ? -1 : GC.GetAllocatedBytesForCurrentThread() - before;
                            before = GC.GetAllocatedBytesForCurrentThread();
                            for (var n = 0; n < 100; n++) found |= !Comparisons.Diff(x, y, null, s).IsEmpty;
                            results[slot++] = found ? -1 : GC.GetAllocatedBytesForCurrentThread() - before;
                        }
                    return results;
                }
            }
            """, OptimizationLevel.Release);
        Assert.Equal(new long[8], deltas);
    }
    internal static ImmutableArray<Difference> Run(string source) => (ImmutableArray<Difference>)Invoke(source);

    static object Invoke(string source, OptimizationLevel level = OptimizationLevel.Debug) =>
        Load(source, level).GetType("Scenario")!.GetMethod("Run")!.Invoke(null, null)!;

    static Assembly Load(string source, OptimizationLevel level = OptimizationLevel.Debug)
    {
        var compilation = Generate(source);
        using var assembly = new MemoryStream();
        var result = compilation.WithOptions(compilation.Options.WithOptimizationLevel(level)).Emit(assembly, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        return Assembly.Load(assembly.ToArray());
    }

    static CSharpCompilation Generate(string source)
    {
        var driver = Driver().RunGeneratorsAndUpdateCompilation(Compilation(source), out var output, out var diagnostics, TestContext.Current.CancellationToken);
        Assert.True(driver.GetRunResult().Results.All(r => r.Exception is null), string.Join(Environment.NewLine, driver.GetRunResult().Results.Select(r => r.Exception)));
        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        var generated = driver.GetRunResult().GeneratedTrees;
        Assert.True(!output.GetDiagnostics().Any(d => d.Severity == DiagnosticSeverity.Error ||
                d.Severity == DiagnosticSeverity.Warning && d.Location.SourceTree is { } tree && generated.Any(g => g.FilePath == tree.FilePath)),
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
