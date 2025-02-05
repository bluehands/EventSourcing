using Microsoft.CodeAnalysis;

namespace EventSourcing.Commands.SourceGenerator;

public class KnownSymbols(Compilation compilation)
{
    public const string CommandExtensionsAttributeName = "EventSourcing.Commands.CommandExtensionsAttribute";

    /// <summary>
    /// The compilation from which information is being queried.
    /// </summary>
    public Compilation Compilation { get; } = compilation;

    public INamedTypeSymbol? CommandExtensionsAttributeOfTErrorTResult => GetOrResolveType($"{CommandExtensionsAttributeName}`2", ref _commandExtensionsAttributeOfTErrorTResult);
    Option<INamedTypeSymbol?> _commandExtensionsAttributeOfTErrorTResult;

    public INamedTypeSymbol? CommandExtensionsAttributeOfTError => GetOrResolveType($"{CommandExtensionsAttributeName}`1", ref _commandExtensionsAttributeOfTError);
    Option<INamedTypeSymbol?> _commandExtensionsAttributeOfTError;

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