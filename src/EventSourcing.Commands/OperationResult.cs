using System;
using System.Collections.Generic;
using System.Linq;
using FunicularSwitch.Generators;

namespace EventSourcing.Funicular.Commands;

[ResultType(errorType: typeof(Failure))]
public abstract partial class OperationResult
{
    public static OperationResult<T> InvalidInput<T>(string message) => Error<T>(Failure.InvalidInput(message));
    public static OperationResult<T> Forbidden<T>(string message) => Error<T>(Failure.Forbidden(message));
    public static OperationResult<T> Conflict<T>(string message) => Error<T>(Failure.Conflict(message));
    public static OperationResult<T> NotFound<T>(string message) => Error<T>(Failure.NotFound(message));
    public static OperationResult<T> InternalError<T>(string message) => Error<T>(Failure.Internal(message));
    public static OperationResult<T> Cancelled<T>(string? message = null) => Error<T>(Failure.Cancelled(message));
}

[UnionType]
public abstract partial record Failure(string Message)
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
}

public static class FailureExtension
{
    [MergeError]
    public static Failure Merge(this Failure f1, Failure f2)
    {
        IReadOnlyCollection<Failure> children;
        if (f1 is Failure.Multiple_ m)
            children = f2 is Failure.Multiple_ m2 ? [..m.Failures, ..m2.Failures] : [..m.Failures, f2];
        else
            children = f2 is Failure.Multiple_ m2 ? [f1, ..m2.Failures] : [f1, f2];

        return Failure.Multiple(children);
    }
}