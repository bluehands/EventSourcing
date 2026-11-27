using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace EventSourcing.Diff.SourceGenerator;

[Generator]
public sealed class DiffGenerator : IIncrementalGenerator
{
    const string AttributeName = "EventSourcing.Diff.DiffAttribute";
    const string Runtime = "global::EventSourcing.Diff.";
    const string Result = "global::System.Collections.Immutable.ImmutableArray<global::EventSourcing.Diff.Difference>";

    static readonly DiagnosticDescriptor InvalidHost = new("ESDIFF001", "Invalid comparison class",
        "'{0}' must be a non-generic, top-level static partial class that is not file-local", "EventSourcing.Diff", DiagnosticSeverity.Error, true);
    static readonly DiagnosticDescriptor UnsupportedType = new("ESDIFF002", "Unsupported comparison type",
        "Cannot generate a comparison for '{0}' (reached via '{1}'): {2}", "EventSourcing.Diff", DiagnosticSeverity.Error, true);
    static readonly DiagnosticDescriptor InvalidCustomMethod = new("ESDIFF003", "Invalid custom comparison",
        "'{0}' must have signature: static ImmutableArray<Difference> Diff(T oldValue, T newValue, PropertyPath? path, DiffSettings? settings)",
        "EventSourcing.Diff", DiagnosticSeverity.Error, true);
    static readonly DiagnosticDescriptor AmbiguousProperty = new("ESDIFF004", "Ambiguous interface property",
        "'{0}' has ambiguous property '{1}' inherited from '{2}' and '{3}'; ignore it or provide a custom comparison",
        "EventSourcing.Diff", DiagnosticSeverity.Error, true);
    static readonly DiagnosticDescriptor UnknownIgnoredProperty = new("ESDIFF005", "Unknown ignored property",
        "Ignored property '{1}' does not match a public instance property of '{0}'", "EventSourcing.Diff", DiagnosticSeverity.Warning, true);
    static readonly DiagnosticDescriptor InvalidMatchBy = new("ESDIFF006", "Invalid MatchBy property",
        "MatchBy '{1}' must name a readable public instance property of '{0}' (or its base types) whose type is not a collection",
        "EventSourcing.Diff", DiagnosticSeverity.Error, true);
    static readonly DiagnosticDescriptor ConflictingMatchBy = new("ESDIFF007", "Conflicting MatchBy values",
        "'{0}' is configured with different MatchBy values in '{1}'", "EventSourcing.Diff", DiagnosticSeverity.Error, true);

    static readonly SymbolDisplayFormat NamespaceFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var hosts = context.SyntaxProvider.ForAttributeWithMetadataName(AttributeName,
            static (node, _) => node is TypeDeclarationSyntax,
            static (attributeContext, _) => (INamedTypeSymbol)attributeContext.TargetSymbol);

        // Model and derived-type edits must invalidate generation even when attributes have not changed.
        context.RegisterSourceOutput(hosts.Collect().Combine(context.CompilationProvider), static (output, input) =>
        {
            var seen = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
            foreach (var host in input.Left)
                if (seen.Add(host)) new Emitter(input.Right, host, output).Emit();
        });
    }

    sealed class Configuration
    {
        public readonly HashSet<string> IgnoredProperties = new(StringComparer.Ordinal);
        public bool Atomic;
        public IPropertySymbol? Key;
        public Location? Location;
    }

    sealed class Emitter(Compilation compilation, INamedTypeSymbol host, SourceProductionContext output)
    {
        readonly Dictionary<ITypeSymbol, Configuration> configurations = new(SymbolEqualityComparer.Default);
        readonly Dictionary<ITypeSymbol, IMethodSymbol> customMethods = new(SymbolEqualityComparer.Default);
        readonly Dictionary<ITypeSymbol, TypeModel> models = new(SymbolEqualityComparer.Default);
        readonly StringBuilder source = new();
        List<INamedTypeSymbol>? localTypes;
        bool failed;

        public void Emit()
        {
            if (!host.IsStatic || host.IsFileLocal || host.Arity != 0 || host.ContainingType is not null ||
                !host.DeclaringSyntaxReferences.All(r => r.GetSyntax(output.CancellationToken) is ClassDeclarationSyntax declaration &&
                    declaration.Modifiers.Any(SyntaxKind.PartialKeyword)))
            {
                output.ReportDiagnostic(Diagnostic.Create(InvalidHost, host.Locations.FirstOrDefault(), host.Name));
                return;
            }

            foreach (var attribute in host.GetAttributes().Where(a => a.AttributeClass?.ToDisplayString() == AttributeName))
            {
                if (attribute.ConstructorArguments.Length == 0 || attribute.ConstructorArguments[0].Value is not ITypeSymbol type) continue;
                var location = attribute.ApplicationSyntaxReference?.GetSyntax(output.CancellationToken).GetLocation() ?? host.Locations.FirstOrDefault();
                if (!configurations.TryGetValue(type, out var configuration)) configurations.Add(type, configuration = new());
                configuration.Location ??= location;
                if (attribute.ConstructorArguments.Length > 1 && !attribute.ConstructorArguments[1].IsNull)
                    foreach (var ignored in attribute.ConstructorArguments[1].Values)
                        if (ignored.Value is string name)
                        {
                            configuration.IgnoredProperties.Add(name);
                            if (!RelatedTypes(type).Any(t => t.GetMembers(name).OfType<IPropertySymbol>()
                                    .Any(p => !p.IsStatic && p.DeclaredAccessibility == Accessibility.Public)))
                                output.ReportDiagnostic(Diagnostic.Create(UnknownIgnoredProperty, location, Name(type), name));
                        }
                configuration.Atomic |= attribute.NamedArguments.Any(p => p.Key == "TreatAsValue" && p.Value.Value is true);
                if (attribute.NamedArguments.FirstOrDefault(p => p.Key == "MatchBy").Value.Value is not string matchBy) continue;
                if (configuration.Key is not null && configuration.Key.Name != matchBy)
                {
                    failed = true;
                    output.ReportDiagnostic(Diagnostic.Create(ConflictingMatchBy, location, Name(type), host.Name));
                    continue;
                }
                var key = RelatedTypes(type).SelectMany(t => t.GetMembers(matchBy).OfType<IPropertySymbol>())
                    .FirstOrDefault(p => !p.IsStatic && !p.IsIndexer && p.GetMethod?.DeclaredAccessibility == Accessibility.Public);
                if (key is null || key.Type.SpecialType != SpecialType.System_String && FindInterface(key.Type, "System.Collections.Generic.IEnumerable<T>") is not null)
                {
                    failed = true;
                    output.ReportDiagnostic(Diagnostic.Create(InvalidMatchBy, location, Name(type), matchBy));
                }
                else configuration.Key = key;
            }

            foreach (var method in host.GetMembers("Diff").OfType<IMethodSymbol>())
            {
                if (!IsCustomComparison(method))
                {
                    failed = true;
                    output.ReportDiagnostic(Diagnostic.Create(InvalidCustomMethod, method.Locations.FirstOrDefault(), method.Name));
                }
                else customMethods[method.Parameters[0].Type] = method;
            }
            foreach (var pair in configurations.ToArray()) Visit(pair.Key, pair.Key.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat), pair.Value.Location);
            if (failed) return;

            Line("// <auto-generated/>");
            Line("#nullable enable");
            if (!host.ContainingNamespace.IsGlobalNamespace)
                Line("namespace " + host.ContainingNamespace.ToDisplayString(NamespaceFormat) + ";");
            Line((host.DeclaredAccessibility == Accessibility.Public ? "public" : "internal") + " static partial class " + Escape(host.Name));
            Line("{");
            foreach (var model in models.Values.OrderBy(m => Name(m.Type), StringComparer.Ordinal))
                EmitMethod(model);
            Line("}");

            // The hash keeps case-only variants apart because hint names are compared case-insensitively.
            var fullName = host.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat.WithMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.None));
            var hash = 2166136261;
            foreach (var c in fullName) hash = unchecked((hash ^ c) * 16777619);
            output.AddSource($"{fullName}.{hash:x8}.Diff.g.cs", source.ToString());
        }

        bool IsCustomComparison(IMethodSymbol method)
        {
            var parameters = method.Parameters;
            return method.IsStatic && !method.IsGenericMethod && !method.IsExtern &&
                (!method.IsPartialDefinition || method.PartialImplementationPart is not null) &&
                parameters.Length == 4 && parameters.All(p => p.RefKind == RefKind.None) &&
                SymbolEqualityComparer.Default.Equals(parameters[0].Type, parameters[1].Type) &&
                IsRuntimeType(parameters[2].Type, "PropertyPath") && IsRuntimeType(parameters[3].Type, "DiffSettings") &&
                method.ReturnType is INamedTypeSymbol result &&
                result.OriginalDefinition.ToDisplayString() == "System.Collections.Immutable.ImmutableArray<T>" &&
                IsRuntimeType(result.TypeArguments[0], "Difference");
        }

        static bool IsRuntimeType(ITypeSymbol type, string name) => type.Name == name &&
            type.ContainingNamespace.ToDisplayString() == "EventSourcing.Diff";

        void Visit(ITypeSymbol original, string via, Location? root)
        {
            output.CancellationToken.ThrowIfCancellationRequested();
            var type = original.WithNullableAnnotation(NullableAnnotation.NotAnnotated);
            if (models.ContainsKey(type)) return;
            if (HasOpenType(type) || type.TypeKind is TypeKind.Error or TypeKind.Pointer or TypeKind.FunctionPointer or TypeKind.Dynamic ||
                type.IsRefLikeType || type is INamedTypeSymbol { IsFileLocal: true } ||
                type.SpecialType == SpecialType.System_Void || type is IArrayTypeSymbol { Rank: not 1 } ||
                !compilation.IsSymbolAccessibleWithin(type, host))
            {
                Fail(type, via, root, "the type must be accessible, closed, and usable as a generic argument; otherwise use [Diff(typeof(...), TreatAsValue = true)] or a custom comparison");
                return;
            }

            var model = new TypeModel(type);
            models.Add(type, model);
            if (customMethods.ContainsKey(type))
            {
                model.Atomic = true;
                return;
            }
            if (IsNullableStruct(type, out var underlying) && (customMethods.ContainsKey(underlying) || !IsAtomic(underlying)))
            {
                model.Underlying = underlying;
                Visit(underlying, via, root);
                return;
            }
            if (IsAtomic(type))
            {
                model.Atomic = true;
                return;
            }
            var dictionary = FindInterface(type, "System.Collections.Generic.IReadOnlyDictionary<TKey, TValue>");
            var readOnlyDictionary = dictionary is not null;
            dictionary ??= FindInterface(type, "System.Collections.Generic.IDictionary<TKey, TValue>");
            if (dictionary is not null)
            {
                model.Dictionary = dictionary;
                model.ReadOnlyDictionary = readOnlyDictionary;
                Visit(dictionary.TypeArguments[1], via + "[]", root);
                return;
            }
            var enumerable = FindInterface(type, "System.Collections.Generic.IEnumerable<T>");
            if (enumerable is not null)
            {
                model.Element = enumerable.TypeArguments[0];
                model.Key = ResolveKey(model.Element);
                if (model.Key is not null) Visit(model.Element, via + "[]", root);
                else if (FindInterface(type, "System.Collections.Generic.IReadOnlySet<T>") is not null) model.Set = "IReadOnlySet";
                else if (FindInterface(type, "System.Collections.Generic.ISet<T>") is not null) model.Set = "ISet";
                else Visit(model.Element, via + "[]", root);
                return;
            }
            if (type.TypeKind is not (TypeKind.Class or TypeKind.Interface))
            {
                Fail(type, via, root, "use [Diff(typeof(...), TreatAsValue = true)] or a custom comparison");
                return;
            }

            var ignored = new HashSet<string>(RelatedTypes(type).Where(configurations.ContainsKey)
                .SelectMany(t => configurations[t].IgnoredProperties), StringComparer.Ordinal);
            var properties = new List<IPropertySymbol>();
            foreach (var group in RelatedTypes(type).Where(t => type.TypeKind == TypeKind.Interface || t.TypeKind != TypeKind.Interface)
                .SelectMany(t => t.GetMembers().OfType<IPropertySymbol>())
                .Where(p => !p.IsStatic && !p.IsIndexer && p.GetMethod?.DeclaredAccessibility == Accessibility.Public &&
                    compilation.IsSymbolAccessibleWithin(p.GetMethod, host) && !ignored.Contains(p.Name))
                .GroupBy(p => p.Name, StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                var candidates = group.ToList();
                var winner = type.TypeKind != TypeKind.Interface ? candidates[0] : candidates.FirstOrDefault(p =>
                    candidates.All(q => SymbolEqualityComparer.Default.Equals(q.ContainingType, p.ContainingType) ||
                        p.ContainingType.AllInterfaces.Contains(q.ContainingType, SymbolEqualityComparer.Default)));
                if (winner is null)
                {
                    failed = true;
                    var first = candidates[0].ContainingType;
                    var second = candidates.First(p => !SymbolEqualityComparer.Default.Equals(p.ContainingType, first)).ContainingType;
                    output.ReportDiagnostic(Diagnostic.Create(AmbiguousProperty, root, Name(type), group.Key, Name(first), Name(second)));
                }
                else properties.Add(winner);
            }
            model.Properties = properties.ToArray();
            foreach (var property in model.Properties) Visit(property.Type, via + "." + property.Name, root);

            if (!type.IsSealed)
            {
                localTypes ??= AllTypes(compilation.Assembly.GlobalNamespace).ToList();
                model.Derived = localTypes.Where(t => !t.IsAbstract && !HasOpenType(t) &&
                        !SymbolEqualityComparer.Default.Equals(t, type) &&
                        RelatedTypes(t).Any(b => SymbolEqualityComparer.Default.Equals(b, type)) &&
                        compilation.IsSymbolAccessibleWithin(t, host))
                    .OrderByDescending(t => RelatedTypes(t).Count()).ThenBy(Name, StringComparer.Ordinal).ToArray();
                foreach (var derived in model.Derived) Visit(derived, via, root);
            }
        }

        IPropertySymbol? ResolveKey(ITypeSymbol element) => RelatedTypes(element)
            .Select(t => configurations.TryGetValue(t, out var configuration) ? configuration.Key : null).FirstOrDefault(k => k is not null);

        static bool IsNullableStruct(ITypeSymbol type, out ITypeSymbol underlying)
        {
            var nullable = type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } named ? named : null;
            underlying = nullable?.TypeArguments[0]!;
            return nullable is not null;
        }

        bool HasCustom(ITypeSymbol type) => customMethods.ContainsKey(type) ||
            IsNullableStruct(type, out var underlying) && customMethods.ContainsKey(underlying);

        bool IsFrameworkType(ITypeSymbol type)
        {
            if (type is not INamedTypeSymbol || SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, compilation.Assembly)) return false;
            var ns = type.ContainingNamespace;
            while (ns is { ContainingNamespace.IsGlobalNamespace: false }) ns = ns.ContainingNamespace;
            return ns?.Name is "System" or "Microsoft" && FindInterface(type, "System.Collections.Generic.IEnumerable<T>") is null;
        }

        bool IsAtomic(ITypeSymbol type) =>
            IsNullableStruct(type, out var underlying) ? IsAtomic(underlying) :
            RelatedTypes(type).Any(t => configurations.TryGetValue(t, out var configuration) && configuration.Atomic) ||
            type.SpecialType is SpecialType.System_String or SpecialType.System_Object ||
            type.TypeKind == TypeKind.Delegate || IsFrameworkType(type) ||
            type.IsValueType && FindInterface(type, "System.Collections.Generic.IEnumerable<T>") is null;

        void EmitMethod(TypeModel model)
        {
            var type = model.Type;
            var parameterType = Name(type) + (type.IsReferenceType ? "?" : "");
            var accessibility = IsPublic(type) ? "public" : "internal";
            var extension = configurations.ContainsKey(type) ? "this " : "";
            var immutableArray = type is INamedTypeSymbol named && named.OriginalDefinition.ToDisplayString() == "System.Collections.Immutable.ImmutableArray<T>";
            var custom = customMethods.ContainsKey(type);
            Line($"    private static void CollectDifferences({parameterType} oldValue, {parameterType} newValue, {Runtime}DiffCollector collector)");
            Line("    {");
            if (custom)
            {
                var bang = type.IsReferenceType ? "!" : "";
                Line($"        collector.AddRange(Diff(oldValue{bang}, newValue{bang}, collector.Path, collector.Settings));");
            }
            else if (model.Atomic)
            {
                Line($"        if (!global::System.Collections.Generic.EqualityComparer<{parameterType}>.Default.Equals(oldValue, newValue)) collector.Changed(oldValue, newValue);");
            }
            else if (model.Underlying is not null)
            {
                Line("        if (!oldValue.HasValue || !newValue.HasValue)");
                Line("        {");
                Line("            if (oldValue.HasValue != newValue.HasValue) collector.Changed(oldValue, newValue);");
                Line("            return;");
                Line("        }");
                Line("        CollectDifferences(oldValue.Value, newValue.Value, collector);");
            }
            else if (model.Dictionary is not null)
            {
                var args = model.Dictionary.TypeArguments;
                var exact = type is INamedTypeSymbol exactType && exactType.OriginalDefinition.ToDisplayString() is
                    "System.Collections.Immutable.ImmutableDictionary<TKey, TValue>" or "System.Collections.Generic.Dictionary<TKey, TValue>";
                var contract = $"global::System.Collections.Generic.{(model.ReadOnlyDictionary ? "IReadOnlyDictionary" : "IDictionary")}<{Name(args[0])}, {NullableName(args[1])}>?";
                var cast = exact ? "" : $"({contract})";
                Line($"        collector.Dictionary<{Name(args[0])}, {NullableName(args[1])}>({cast}oldValue, {cast}newValue, CollectDifferences);");
            }
            else if (model.Element is not null)
            {
                if (immutableArray)
                {
                    Line("        if (oldValue.IsDefault || newValue.IsDefault)");
                    Line("        {");
                    Line("            if (!oldValue.Equals(newValue)) collector.Changed(oldValue, newValue);");
                    Line("            return;");
                    Line("        }");
                }
                var element = NullableName(model.Element);
                var atomic = IsAtomic(model.Element) && !HasCustom(model.Element) ? "true" : "false";
                if (model.Key is not null)
                {
                    var keyType = IsNullableStruct(model.Key.Type, out _) ? "object" : Name(model.Key.Type);
                    if (immutableArray) Line("        if (oldValue == newValue) return;");
                    Line($"        collector.Keyed<{element}, {keyType}>(oldValue, newValue, static x => (({Name(model.Key.ContainingType)})x!).{Escape(model.Key.Name)}!, CollectDifferences);");
                }
                else if (model.Set is not null)
                {
                    var contract = $"global::System.Collections.Generic.{model.Set}<{element}>?";
                    Line($"        collector.Set<{element}>(({contract})oldValue, ({contract})newValue);");
                }
                else if (immutableArray || type is IArrayTypeSymbol)
                    Line($"        collector.Array<{element}>(oldValue, newValue, CollectDifferences, {atomic});");
                else if (FindInterface(type, "System.Collections.Generic.IReadOnlyList<T>") is { } list && SymbolEqualityComparer.Default.Equals(list.TypeArguments[0], model.Element))
                {
                    var contract = $"global::System.Collections.Generic.IReadOnlyList<{element}>?";
                    Line($"        collector.List<{element}>(({contract})oldValue, ({contract})newValue, CollectDifferences, {atomic});");
                }
                else
                    Line($"        collector.Enumerable<{element}>(oldValue, newValue, CollectDifferences, {atomic});");
            }
            else
            {
                Line("        if (global::System.Object.ReferenceEquals(oldValue, newValue)) return;");
                Line("        if (oldValue is null || newValue is null || oldValue.GetType() != newValue.GetType())");
                Line("        {");
                Line("            collector.Changed(oldValue, newValue);");
                Line("            return;");
                Line("        }");
                for (var index = 0; index < model.Derived.Length; index++)
                {
                    var derived = Name(model.Derived[index]);
                    Line($"        if (oldValue is {derived} derived{index})");
                    Line("        {");
                    Line($"            CollectDifferences(derived{index}, ({derived})newValue, collector);");
                    Line("            return;");
                    Line("        }");
                }
                Line($"        if (oldValue.GetType() != typeof({Name(type)})) throw new global::System.NotSupportedException($\"Runtime type '{{oldValue.GetType()}}' at '{{collector.Path}}' is not known to the diff generator. Declare a comparison in its assembly or provide a custom comparison.\");");
                foreach (var property in model.Properties)
                {
                    var name = Escape(property.Name);
                    var literal = SymbolDisplay.FormatLiteral(property.Name, true);
                    var propertyType = NullableName(property.Type);
                    var hasCustom = HasCustom(property.Type);
                    if (!hasCustom && models.TryGetValue(property.Type.WithNullableAnnotation(NullableAnnotation.NotAnnotated), out var propertyModel) && propertyModel.Atomic)
                        Line($"        if (!global::System.Collections.Generic.EqualityComparer<{propertyType}>.Default.Equals(oldValue.{name}, newValue.{name})) collector.Changed({literal}, oldValue.{name}, newValue.{name});");
                    else
                    {
                        var shortcut = property.Type.IsReferenceType && !hasCustom ? $"!global::System.Object.ReferenceEquals(oldValue.{name}, newValue.{name}) && " : "";
                        Line($"        if ({shortcut}collector.Enter({literal}))");
                        Line("        {");
                        Line($"            CollectDifferences(oldValue.{name}, newValue.{name}, collector);");
                        Line("            collector.Exit();");
                        Line("        }");
                    }
                    Line("        if (collector.Stopped) return;");
                }
            }
            Line("    }");
            if (!custom)
                Line($"    {accessibility} static {Result} Diff({extension}{parameterType} oldValue, {parameterType} newValue, {Runtime}PropertyPath? path = null, {Runtime}DiffSettings? settings = null)" +
                    $" => {Runtime}DiffCollector.Collect<{parameterType}>(oldValue, newValue, path, settings, CollectDifferences);");
            if (!host.GetMembers("HasDifferences").OfType<IMethodSymbol>().Any(m => m.Parameters.Length >= 2 &&
                    SymbolEqualityComparer.Default.Equals(m.Parameters[0].Type, type) && SymbolEqualityComparer.Default.Equals(m.Parameters[1].Type, type)))
                Line($"    {accessibility} static bool HasDifferences({extension}{parameterType} oldValue, {parameterType} newValue, {Runtime}DiffSettings? settings = null)" +
                    $" => {Runtime}DiffCollector.HasDifferences<{parameterType}>(oldValue, newValue, null, settings, CollectDifferences);");
        }

        void Fail(ITypeSymbol type, string via, Location? root, string reason)
        {
            failed = true;
            output.ReportDiagnostic(Diagnostic.Create(UnsupportedType, root ?? host.Locations.FirstOrDefault(), Name(type), via, reason));
        }

        void Line(string line) => source.AppendLine(line);
    }

    sealed class TypeModel(ITypeSymbol type)
    {
        public readonly ITypeSymbol Type = type;
        public bool Atomic;
        public INamedTypeSymbol? Dictionary;
        public bool ReadOnlyDictionary;
        public ITypeSymbol? Underlying;
        public IPropertySymbol? Key;
        public string? Set;
        public ITypeSymbol? Element;
        public IPropertySymbol[] Properties = [];
        public INamedTypeSymbol[] Derived = [];
    }

    static INamedTypeSymbol? FindInterface(ITypeSymbol type, string definition) =>
        type is INamedTypeSymbol named && named.OriginalDefinition.ToDisplayString() == definition ? named :
            type.AllInterfaces.FirstOrDefault(i => i.OriginalDefinition.ToDisplayString() == definition);

    static IEnumerable<ITypeSymbol> RelatedTypes(ITypeSymbol type)
    {
        for (ITypeSymbol? current = type; current is not null; current = current.BaseType) yield return current;
        foreach (var contract in type.AllInterfaces) yield return contract;
    }

    static IEnumerable<INamedTypeSymbol> AllTypes(INamespaceOrTypeSymbol parent)
    {
        foreach (var member in parent.GetMembers().OfType<INamespaceOrTypeSymbol>())
        {
            if (member is INamedTypeSymbol type) yield return type;
            foreach (var nested in AllTypes(member)) yield return nested;
        }
    }

    static bool HasOpenType(ITypeSymbol type) => type.TypeKind == TypeKind.TypeParameter ||
        type is INamedTypeSymbol named && (named.IsUnboundGenericType || named.TypeArguments.Any(HasOpenType) ||
            named.ContainingType is not null && HasOpenType(named.ContainingType)) ||
        type is IArrayTypeSymbol array && HasOpenType(array.ElementType);

    static bool IsPublic(ITypeSymbol type) => type is IArrayTypeSymbol array ? IsPublic(array.ElementType) :
        type.DeclaredAccessibility == Accessibility.Public &&
        (type.ContainingType is null || IsPublic(type.ContainingType)) &&
        (type is not INamedTypeSymbol named || named.TypeArguments.All(IsPublic));

    static string Name(ITypeSymbol type) => type.WithNullableAnnotation(NullableAnnotation.NotAnnotated)
        .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
            SymbolDisplayFormat.FullyQualifiedFormat.MiscellaneousOptions | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier));

    static string NullableName(ITypeSymbol type) => Name(type) +
        (type.IsReferenceType && type.NullableAnnotation == NullableAnnotation.Annotated ? "?" : "");

    static string Escape(string identifier) => "@" + identifier;
}
