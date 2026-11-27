param(
    [Parameter(Mandatory)][string] $PackageDirectory,
    [Parameter(Mandatory)][string] $WorkingDirectory
)

$ErrorActionPreference = 'Stop'
$PackageDirectory = (Resolve-Path $PackageDirectory).Path
$package = @(Get-ChildItem $PackageDirectory -Filter 'Bluehands.EventSourcing.Diff.*.nupkg')
if ($package.Count -ne 1) { throw 'Expected exactly one Bluehands.EventSourcing.Diff package.' }

$archive = [System.IO.Compression.ZipFile]::OpenRead($package[0].FullName)
try {
    $reader = [System.IO.StreamReader]::new($archive.GetEntry('Bluehands.EventSourcing.Diff.nuspec').Open())
    try { [xml] $manifest = $reader.ReadToEnd() } finally { $reader.Dispose() }
    $version = $manifest.package.metadata.version
    $files = @($archive.Entries.FullName)
    if ('analyzers/dotnet/cs/EventSourcing.Diff.SourceGenerator.dll' -notin $files) { throw 'Analyzer missing from package.' }
    if ('lib/net10.0/EventSourcing.Diff.dll' -notin $files) { throw 'Runtime missing from package.' }
    if ('lib/net10.0/EventSourcing.Diff.xml' -notin $files) { throw 'XML documentation missing from package.' }
    if ($manifest.package.metadata.dependencies.group.dependency) { throw 'The package must have no application dependencies.' }
} finally { $archive.Dispose() }

# Use a fresh child so the check never overwrites an existing consumer or reuses a cached package.
$root = Join-Path $WorkingDirectory ([Guid]::NewGuid().ToString('N'))
$models = New-Item -ItemType Directory -Path (Join-Path $root 'Models') -Force
$consumer = New-Item -ItemType Directory -Path (Join-Path $root 'Consumer') -Force
$escapedSource = [System.Security.SecurityElement]::Escape($PackageDirectory)
@'
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <RestoreSources>__SOURCE__</RestoreSources>
    <RestorePackagesPath>$(MSBuildThisFileDirectory)packages</RestorePackagesPath>
    <NuGetAudit>false</NuGetAudit>
  </PropertyGroup>
  <ItemGroup><PackageReference Include="Bluehands.EventSourcing.Diff" Version="__VERSION__" /></ItemGroup>
</Project>
'@.Replace('__SOURCE__', $escapedSource).Replace('__VERSION__', $version) | Set-Content (Join-Path $root 'Directory.Build.props')

'<Project Sdk="Microsoft.NET.Sdk" />' | Set-Content (Join-Path $models 'Models.csproj')
@'
using EventSourcing.Diff;
namespace Models;
public record Address(string City);
public record Base(int Value);
[Diff(typeof(Address)), Diff(typeof(Base))]
public static partial class Comparisons;
'@ | Set-Content (Join-Path $models 'Models.cs')

@'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType></PropertyGroup>
  <ItemGroup><ProjectReference Include="../Models/Models.csproj" /></ItemGroup>
</Project>
'@ | Set-Content (Join-Path $consumer 'Consumer.csproj')
@'
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using EventSourcing.Diff;
using Models;

ImmutableArray<Difference> fromModels = Models.Comparisons.Diff(new Address("old"), new Address("new"));
ImmutableArray<Difference> fromConsumer = ConsumerComparisons.Diff(new State(new("old")), new State(new("new")));
if (fromModels.Length != 1 || fromConsumer.Length != 1 || fromConsumer[0].PropertyPath.ToString() != "Address.City")
    throw new Exception("Cross-assembly package comparison failed.");
try
{
    Models.Comparisons.Diff((Base)new External(1, "old"), new External(1, "new"));
    throw new Exception("Unknown runtime subclass was silently ignored.");
}
catch (NotSupportedException) { }

var error = new InvalidOperationException("boom");
Document Doc(Exception? e, HashSet<string> tags, params Line[] lines) => new(e, tags, [.. lines]);
if (DocumentComparisons.Diff(Doc(error, ["a", "b"], new Line(1, 1)), Doc(error, ["b", "a"], new Line(1, 1))).Length != 0)
    throw new Exception("Equal exception or reordered set reported a difference.");
ImmutableArray<Difference> added = DocumentComparisons.Diff(Doc(null, ["a"], new Line(1, 1)), Doc(null, ["a", "b"], new Line(1, 1)));
if (added.Length != 1 || added[0].Kind != DifferenceKind.Added || added[0].PropertyPath.ToString() != "Tags")
    throw new Exception("Set addition was not reported as Added.");
if (DocumentComparisons.Diff(Doc(null, [], new Line(1, 1), new Line(2, 2)), Doc(null, [], new Line(2, 2), new Line(1, 1))).Length != 0)
    throw new Exception("Reordered keyed list reported a difference.");
ImmutableArray<Difference> keyed = DocumentComparisons.Diff(Doc(null, [], new Line(1, 1), new Line(2, 2)), Doc(null, [], new Line(2, 5), new Line(1, 1)));
if (keyed.Length != 1 || keyed[0].Kind != DifferenceKind.Changed || keyed[0].PropertyPath.ToString() != "Lines[2].Quantity")
    throw new Exception("Keyed element change had an unexpected path.");
Console.WriteLine("Package smoke check passed: generator loading, XML docs, shared types, nested comparisons, external subtype rejection, framework types, sets, keyed matching.");

public record Line(int Id, int Quantity);
public record Document(Exception? Error, HashSet<string> Tags, List<Line> Lines);
[Diff(typeof(Document)), Diff(typeof(Line), MatchBy = nameof(Line.Id))]
public static partial class DocumentComparisons;
public record State(Address Address);
public record External(int Value, string Extra) : Base(Value);
[Diff(typeof(State))]
public static partial class ConsumerComparisons;
'@ | Set-Content (Join-Path $consumer 'Program.cs')

dotnet run --project (Join-Path $consumer 'Consumer.csproj') --configuration Release
if ($LASTEXITCODE -ne 0) { throw "Package consumer failed with exit code $LASTEXITCODE. Files retained in $root" }
