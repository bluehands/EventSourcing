using EventSourcing.Funicular.Commands;
using System.Diagnostics.CodeAnalysis;
using FunicularSwitch.Generators;

namespace Meetup;

[ResultType(typeof(Failure))]
public partial class Result<T>
{

}

[FunicularSwitch.Generators.UnionType]
public abstract partial record Failure(string Message) : IFailure<Failure>
{
    public record Forbidden_(string Message) : Failure(Message);

    public record NotFound_(string Message) : Failure(Message);

    public record Conflict_(string Message) : Failure(Message);

    public record Internal_(string Message) : Failure(Message);

    public record InvalidInput_(string Message) : Failure(Message);

    public record Multiple_(IReadOnlyCollection<Failure> Failures)
        : Failure(string.Join(Environment.NewLine, Failures.Select(f => f.Message)))
    {
        public IReadOnlyCollection<Failure> Failures { get; } = Failures;
    }

    public record Cancelled_ : Failure
    {
        public Cancelled_(string? message = null) : base(message ?? "Operation cancelled")
        {
        }
    }

    public override string ToString() => $"{GetType().Name.TrimEnd('_')}: {Message}";

    public bool IsMultiple([NotNullWhen(true)] out IReadOnlyCollection<Failure>? failures)
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
    public static Failure MergeDefaultFailure(this Failure f1, Failure f2)
    {
        IReadOnlyCollection<Failure> children;
        if (f1.IsMultiple(out var m))
            children = f2.IsMultiple(out var m2) ? [..m, ..m2] : [..m, f2];
        else
            children = f2.IsMultiple(out var m2) ? [f1, ..m2] : [f1, f2];

        return Failure.Multiple(children);
    }
}