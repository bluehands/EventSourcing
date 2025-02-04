using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using FunicularSwitch.Generators;

namespace EventSourcing.Commands.Templates;

[UnionType]
public abstract partial record FailureTypeName(string Message) : IFailure<FailureTypeName>
{
    public record Forbidden_(string Message) : FailureTypeName(Message);

    public record NotFound_(string Message) : FailureTypeName(Message);

    public record Conflict_(string Message) : FailureTypeName(Message);

    public record Internal_(string Message) : FailureTypeName(Message);

    public record InvalidInput_(string Message) : FailureTypeName(Message);

    public record Multiple_(IReadOnlyCollection<FailureTypeName> Failures)
        : FailureTypeName(string.Join(Environment.NewLine, Failures.Select(f => f.Message)))
    {
        public IReadOnlyCollection<FailureTypeName> Failures { get; } = Failures;
    }

    public record Cancelled_ : FailureTypeName
    {
        public Cancelled_(string? message = null) : base(message ?? "Operation cancelled")
        {
        }
    }

    public override string ToString() => $"{GetType().Name.TrimEnd('_')}: {Message}";

    public bool IsMultiple([NotNullWhen(true)] out IReadOnlyCollection<FailureTypeName>? failures)
    {
        if (this is Multiple_ m)
        {
            failures = m.Failures;
            return true;
        }

        failures = null;
        return false;
    }
}

public static class DefaultFailureExtensions
{
    [MergeError]
    public static FailureTypeName MergeDefaultFailure(this FailureTypeName f1, FailureTypeName f2)
    {
        IReadOnlyCollection<FailureTypeName> children;
        if (f1.IsMultiple(out var m))
            children = f2.IsMultiple(out var m2) ? [..m, ..m2] : [..m, f2];
        else
            children = f2.IsMultiple(out var m2) ? [f1, ..m2] : [f1, f2];

        return FailureTypeName.Multiple(children);
    }
}