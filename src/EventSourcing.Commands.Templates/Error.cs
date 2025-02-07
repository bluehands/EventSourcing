using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using FunicularSwitch.Generators;

namespace EventSourcing.Commands.Templates;

[UnionType]
public abstract partial record ErrorTypeName(string Message) : IError<ErrorTypeName>
{
    public record Forbidden_(string Message) : ErrorTypeName(Message);

    public record NotFound_(string Message) : ErrorTypeName(Message);

    public record Conflict_(string Message) : ErrorTypeName(Message);

    public record Internal_(string Message) : ErrorTypeName(Message);

    public record InvalidInput_(string Message) : ErrorTypeName(Message);

    public record Multiple_(IReadOnlyCollection<ErrorTypeName> Errors)
        : ErrorTypeName(string.Join(Environment.NewLine, Errors.Select(f => f.Message)))
    {
        public IReadOnlyCollection<ErrorTypeName> Errors { get; } = Errors;
    }

    public record Cancelled_ : ErrorTypeName
    {
        public Cancelled_(string? message = null) : base(message ?? "Operation cancelled")
        {
        }
    }

    public override string ToString() => $"{GetType().Name.TrimEnd('_')}: {Message}";

    public bool IsMultiple([NotNullWhen(true)] out IReadOnlyCollection<ErrorTypeName>? errors)
    {
        if (this is Multiple_ m)
        {
            errors = m.Errors;
            return true;
        }

        errors = null;
        return false;
    }
}

public static class DefaultErrorExtensions
{
    [MergeError]
    public static ErrorTypeName Merge(this ErrorTypeName f1, ErrorTypeName f2)
    {
        IReadOnlyCollection<ErrorTypeName> children;
        if (f1.IsMultiple(out var m))
            children = f2.IsMultiple(out var m2) ? [..m, ..m2] : [..m, f2];
        else
            children = f2.IsMultiple(out var m2) ? [f1, ..m2] : [f1, f2];

        return ErrorTypeName.Multiple(children);
    }
}