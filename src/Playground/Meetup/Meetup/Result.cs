using System.Diagnostics.CodeAnalysis;
using EventSourcing.Commands;
using FunicularSwitch.Generators;

namespace Meetup;

[ResultType(typeof(Error))]
public partial class Result<T>
{
}

[FunicularSwitch.Generators.UnionType]
public abstract partial record Error(string Message) : IError<Error>
{
    public record Forbidden_(string Message) : Error(Message);

    public record NotFound_(string Message) : Error(Message);

    public record Conflict_(string Message) : Error(Message);

    public record Internal_(string Message) : Error(Message);

    public record InvalidInput_(string Message) : Error(Message);

    public record Multiple_(IReadOnlyCollection<Error> Errors)
        : Error(string.Join(Environment.NewLine, Errors.Select(f => f.Message)))
    {
        public IReadOnlyCollection<Error> Errors { get; } = Errors;
    }

    public record Cancelled_ : Error
    {
        public Cancelled_(string? message = null) : base(message ?? "Operation cancelled")
        {
        }
    }

    public override string ToString() => $"{GetType().Name.TrimEnd('_')}: {Message}";

    public bool IsMultiple([NotNullWhen(true)] out IReadOnlyCollection<Error>? errors)
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

public static class ErrorExtensions
{
    [MergeError]
    public static Error Merge(this Error f1, Error f2)
    {
        IReadOnlyCollection<Error> children;
        if (f1.IsMultiple(out var m))
            children = f2.IsMultiple(out var m2) ? [..m, ..m2] : [..m, f2];
        else
            children = f2.IsMultiple(out var m2) ? [f1, ..m2] : [f1, f2];

        return Error.Multiple(children);
    }
}