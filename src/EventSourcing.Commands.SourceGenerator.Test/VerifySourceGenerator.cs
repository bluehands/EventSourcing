using System.Runtime.CompilerServices;
using AwesomeAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace EventSourcing.Commands.SourceGenerator.Test;

public abstract class VerifySourceGenerator
{
    [ModuleInitializer]
    public static void InitializeSnapshots() => VerifySourceGenerators.Initialize();

    protected Task Verify(string source, int expectedSourceCount, [CallerFilePath] string sourceFile = "")
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.CSharp14);
        var frameworkAssemblies = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator);
        var assemblyPaths = frameworkAssemblies.Concat([
            typeof(ICommandBus).Assembly.Location,
            typeof(Event).Assembly.Location,
            typeof(System.Reactive.Unit).Assembly.Location
        ]).Distinct(StringComparer.OrdinalIgnoreCase);
        var compilation = CSharpCompilation.Create("GeneratorTests",
            [CSharpSyntaxTree.ParseText(source, parseOptions)],
            assemblyPaths.Select(path => MetadataReference.CreateFromFile(path)),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new Generator().AsSourceGenerator()], parseOptions: parseOptions);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var updatedCompilation, out var diagnostics);
        var result = driver.GetRunResult();
        result.Results.Should().OnlyContain(r => r.Exception == null);
        diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
        result.GeneratedTrees.Should().HaveCount(expectedSourceCount);
        var errors = updatedCompilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error);
        errors.Should().BeEmpty("the generated consumer must compile; generated sources: {0}",
            string.Join(Environment.NewLine, result.GeneratedTrees.Select(t => $"{t.FilePath}\n{t}")));

        return VerifyXunit.Verifier.Verify(driver, sourceFile: sourceFile).UseDirectory("Snapshots");
    }
}
