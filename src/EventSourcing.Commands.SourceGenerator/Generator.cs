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
            attributeFullyQualifiedNames: [$"{KnownSymbols.CommandExtensionsAttributeName}<TError, TResult>", $"{KnownSymbols.CommandExtensionsAttributeName}<TError>"],
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
                ITypeSymbol errorTypeSymbol;

                if (attributeData.AttributeClass is
                        { TypeArguments: [{ } resultType, { } errorType] } &&
                    SymbolEqualityComparer.Default.Equals(attributeData.AttributeClass!.ConstructedFrom,
                        knownSymbols.CommandExtensionsAttributeOfTErrorTResult))
                {
                    errorTypeSymbol = errorType;
                    resultTypeName = resultType.Name;
                    resultTypeNamespace = resultType.ContainingNamespace.ToDisplayString(RoslynHelpers.QualifiedNameOnlyFormat);
                    addPartialResultImplementingIResult = resultType.DeclaringSyntaxReferences.Length > 0 &&
                                                          resultType.DeclaringSyntaxReferences[0].GetSyntax(token) is TypeDeclarationSyntax d && d.Modifiers.Any(m => m.Text == "partial") &&
                                                          SymbolEqualityComparer.Default.Equals(resultType.ContainingAssembly, extensionType.ContainingAssembly);
                   
                }
                else if (attributeData.AttributeClass is
                             { TypeArguments: [{ } errorType1] } &&
                         SymbolEqualityComparer.Default.Equals(attributeData.AttributeClass!.ConstructedFrom,
                             knownSymbols.CommandExtensionsAttributeOfTError))
                {
                    errorTypeSymbol = errorType1;
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
                    ErrorFullTypeName: errorTypeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
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
            var errorExtensionsSource = TemplatesContents.ErrorTypeExtensions
                .Replace("namespace EventSourcing.Commands.Templates", $"namespace {resultModel.TargetNamespace}")
                .Replace("public static partial class CommandBusExtensions", $"{string.Join(" ", resultModel.ExtensionTypeModifiers)} class {resultModel.ExtensionTypeName}")
                .Replace("ErrorTypeName", resultModel.ErrorFullTypeName);

            spc.AddSource($"{resultModel.TargetNamespace}.{resultModel.ExtensionTypeName}.ErrorExtensions.g.cs", errorExtensionsSource);

            if (resultModel.ResultFullTypeName != null)
            {
                var resultExtensionsSource = TemplatesContents.ResultTypeExtensions
                    .Replace("namespace EventSourcing.Commands.Templates", $"namespace {resultModel.TargetNamespace}")
                    .Replace("public static partial class CommandBusExtensions", $"{string.Join(" ", resultModel.ExtensionTypeModifiers)} class {resultModel.ExtensionTypeName}")
                    .Replace("ResultTypeName", resultModel.ResultFullTypeName)
                    .Replace("ErrorTypeName", resultModel.ErrorFullTypeName);

                if (resultModel.AddPartialResultImplementingIResult)
                {
                    var partialResult = 
                        $$"""
                          namespace {{resultModel.ResultTypeNamespace}}
                          {
                             public partial class {{resultModel.ResultTypeName}}<T> : EventSourcing.Commands.IResult<T, {{resultModel.ErrorFullTypeName}}, {{resultModel.ResultFullTypeName}}<T>>{};
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
    string ErrorFullTypeName,
    string? ResultTypeName,
    string? ResultTypeNamespace,
    bool AddPartialResultImplementingIResult
)
{
    public string? ResultFullTypeName { get; } = ResultTypeName != null ? $"global::{ResultTypeNamespace}.{ResultTypeName}" : null;
}