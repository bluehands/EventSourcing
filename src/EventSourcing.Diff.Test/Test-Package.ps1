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
Console.WriteLine("Package smoke check passed: generator loading, shared types, nested comparisons, external subtype rejection.");

public record State(Address Address);
public record External(int Value, string Extra) : Base(Value);
[Diff(typeof(State))]
public static partial class ConsumerComparisons;
'@ | Set-Content (Join-Path $consumer 'Program.cs')

dotnet run --project (Join-Path $consumer 'Consumer.csproj') --configuration Release
if ($LASTEXITCODE -ne 0) { throw "Package consumer failed with exit code $LASTEXITCODE. Files retained in $root" }
