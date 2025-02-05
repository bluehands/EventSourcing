using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using EventSourcing.Commands.SourceGenerator.Templates;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using PolyType.Roslyn;
using PolyType.SourceGenerator.Helpers;

namespace EventSourcing.Commands.SourceGenerator;

[Generator]
public class Generator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var knownSymbols = context.CompilationProvider
            .Select((compilation, _) => new KnownSymbols(compilation));

        var model = context.SyntaxProvider.ForTypesWithAttributeDeclarations(
            attributeFullyQualifiedNames: [$"{KnownSymbols.CommandExtensionsAttributeName}<TFailure, TResult>", $"{KnownSymbols.CommandExtensionsAttributeName}<TFailure>"],
            (syntax, _) => syntax is TypeDeclarationSyntax)
            .Collect()
            .Combine(knownSymbols)
            .Select((tuple, token) => ExtractModel(tuple.Left, tuple.Right, token));
            
        context.RegisterSourceOutput(model, GenerateSource);
    }

    static ResultProviderModel ExtractModel(ImmutableArray<TypeWithAttributeDeclarationContext> attributedTypes, KnownSymbols knownSymbols, CancellationToken token)
    {
        List<ResultModel> resultModels = [];

        foreach (var attributedType in attributedTypes)
        {
            var extensionType = attributedType.TypeSymbol;
            var targetNamespace = extensionType.ContainingNamespace.ToDisplayString(RoslynHelpers.QualifiedNameOnlyFormat);

            foreach (var attributeData in extensionType.GetAttributes())
            {
                string? resultTypeName = null;
                string? resultTypeNamespace = null;
                bool addPartialResultImplementingIResult = false;
                ITypeSymbol failureTypeSymbol;

                if (attributeData.AttributeClass is
                        { TypeArguments: [{ } resultType, { } failureType] } &&
                    SymbolEqualityComparer.Default.Equals(attributeData.AttributeClass!.ConstructedFrom,
                        knownSymbols.CommandExtensionsAttributeOfTFailureTResult))
                {
                    failureTypeSymbol = failureType;
                    resultTypeName = resultType.Name;
                    resultTypeNamespace = resultType.ContainingNamespace.ToDisplayString(RoslynHelpers.QualifiedNameOnlyFormat);
                    addPartialResultImplementingIResult = resultType.DeclaringSyntaxReferences.Length > 0 &&
                                                          resultType.DeclaringSyntaxReferences[0].GetSyntax(token) is TypeDeclarationSyntax d && d.Modifiers.Any(m => m.Text == "partial") &&
                                                          SymbolEqualityComparer.Default.Equals(resultType.ContainingAssembly, extensionType.ContainingAssembly);
                   
                }
                else if (attributeData.AttributeClass is
                             { TypeArguments: [{ } failureType1] } &&
                         SymbolEqualityComparer.Default.Equals(attributeData.AttributeClass!.ConstructedFrom,
                             knownSymbols.CommandExtensionsAttributeOfTFailure))
                {
                    failureTypeSymbol = failureType1;
                }
                else continue;

                resultModels.Add(new(targetNamespace,
                    ExtensionTypeModifiers: attributedType.Declarations
                        .First().Syntax.Modifiers
                        .Select(m => m.Text)
                        .ToImmutableEquatableArray(),
                    ExtensionTypeName: extensionType.Name,
                    ResultTypeName: resultTypeName,
                    ResultTypeNamespace: resultTypeNamespace,
                    FailureFullTypeName: failureTypeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    AddPartialResultImplementingIResult: 
                    addPartialResultImplementingIResult
                ));
            }
        }

        return new()
        {
            ResultModels = resultModels.ToImmutableEquatableArray()
        };
    }

    static void GenerateSource(SourceProductionContext spc, ResultProviderModel? model)
    {
        if (model == null)
            return;

        foreach (var resultModel in model.ResultModels)
        {
            var failureExtensionsSource = TemplatesContents.FailureTypeExtensions
                .Replace("namespace EventSourcing.Commands.Templates", $"namespace {resultModel.TargetNamespace}")
                .Replace("public static partial class CommandBusExtensions", $"{string.Join(" ", resultModel.ExtensionTypeModifiers)} class {resultModel.ExtensionTypeName}")
                .Replace("FailureTypeName", resultModel.FailureFullTypeName);

            spc.AddSource($"{resultModel.TargetNamespace}.{resultModel.ExtensionTypeName}.FailureExtensions.g.cs", failureExtensionsSource);

            if (resultModel.ResultFullTypeName != null)
            {
                var resultExtensionsSource = TemplatesContents.ResultTypeExtensions
                    .Replace("namespace EventSourcing.Commands.Templates", $"namespace {resultModel.TargetNamespace}")
                    .Replace("public static partial class CommandBusExtensions", $"{string.Join(" ", resultModel.ExtensionTypeModifiers)} class {resultModel.ExtensionTypeName}")
                    .Replace("ResultTypeName", resultModel.ResultFullTypeName)
                    .Replace("FailureTypeName", resultModel.FailureFullTypeName);

                if (resultModel.AddPartialResultImplementingIResult)
                {
                    var partialResult = 
                        $$"""
                          namespace {{resultModel.ResultTypeNamespace}}
                          {
                             public partial class {{resultModel.ResultTypeName}}<T> : EventSourcing.Commands.IResult<T, {{resultModel.FailureFullTypeName}}, {{resultModel.ResultFullTypeName}}<T>>{};
                          }
                          """;
                    resultExtensionsSource += partialResult;
                }

                spc.AddSource($"{resultModel.TargetNamespace}.{resultModel.ExtensionTypeName}.ResultExtensions.g.cs", resultExtensionsSource);
            }
        }
    }
}

sealed record ResultProviderModel
{
    public required ImmutableEquatableArray<ResultModel> ResultModels { get; init; }
}

sealed record ResultModel(
    string TargetNamespace,
    ImmutableEquatableArray<string> ExtensionTypeModifiers,
    string ExtensionTypeName,
    string FailureFullTypeName,
    string? ResultTypeName,
    string? ResultTypeNamespace,
    bool AddPartialResultImplementingIResult
)
{
    public string? ResultFullTypeName { get; } = ResultTypeName != null ? $"global::{ResultTypeNamespace}.{ResultTypeName}" : null;
}

public class KnownSymbols(Compilation compilation)
{
    public const string CommandExtensionsAttributeName = "EventSourcing.Commands.CommandExtensionsAttribute";

    /// <summary>
    /// The compilation from which information is being queried.
    /// </summary>
    public Compilation Compilation { get; } = compilation;

    public INamedTypeSymbol? CommandExtensionsAttributeOfTFailureTResult => GetOrResolveType($"{CommandExtensionsAttributeName}`2", ref _commandExtensionsAttributeOfTFailureTResult);
    Option<INamedTypeSymbol?> _commandExtensionsAttributeOfTFailureTResult;

    public INamedTypeSymbol? CommandExtensionsAttributeOfTFailure => GetOrResolveType($"{CommandExtensionsAttributeName}`1", ref _commandExtensionsAttributeOfTFailure);
    Option<INamedTypeSymbol?> _commandExtensionsAttributeOfTFailure;

    public INamedTypeSymbol? ResultInterface => GetOrResolveType("EventSourcing.Commands.IResult`3", ref _resultInterface);
    Option<INamedTypeSymbol?> _resultInterface;


    /// <summary>
    /// Get or resolve a type by its fully qualified name.
    /// </summary>
    /// <param name="fullyQualifiedName">The fully qualified name of the type to resolve.</param>
    /// <param name="field">A field in which to cache the result for future use.</param>
    /// <returns>The type symbol result or null if not found.</returns>
    protected INamedTypeSymbol? GetOrResolveType(string fullyQualifiedName, ref Option<INamedTypeSymbol?> field)
    {
        if (field.HasValue)
        {
            return field.Value;
        }

        INamedTypeSymbol? type = Compilation.GetTypeByMetadataName(fullyQualifiedName);
        field = new(type);
        return type;
    }

    /// <summary>
    /// Defines a true optional type that supports Some(null) representations.
    /// </summary>
    /// <typeparam name="T">The optional value contained.</typeparam>
    protected readonly struct Option<T>(T value)
    {
        /// <summary>
        /// Indicates whether the option has a value, or <see langword="default" /> otherwise.
        /// </summary>
        public bool HasValue { get; } = true;
        /// <summary>
        /// The value of the option.
        /// </summary>
        public T Value { get; } = value;
    }
}

/// <summary>
/// Represents a cacheable type identifier that uses FQN to derive equality.
/// </summary>
public readonly struct TypeId : IEquatable<TypeId>
{
    public required string FullyQualifiedName { get; init; }
    public required bool IsValueType { get; init; }
    public required SpecialType SpecialType { get; init; }

    public bool Equals(TypeId other) => FullyQualifiedName == other.FullyQualifiedName;
    public override int GetHashCode() => FullyQualifiedName.GetHashCode();
    public override bool Equals(object? obj) => obj is TypeId other && Equals(other);
    public static bool operator ==(TypeId left, TypeId right) => left.Equals(right);
    public static bool operator !=(TypeId left, TypeId right) => !(left == right);
    public override string ToString() => FullyQualifiedName;
}

