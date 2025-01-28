using System.Collections.Generic;

namespace EventSourcing.Funicular.Commands;

public static class FailureExtension
{
    public static TFailure Merge<TFailure>(this TFailure f1, TFailure f2)
        where TFailure : IFailure<TFailure>
    {
        IReadOnlyCollection<TFailure> children;
        if (f1.IsMultiple(out var m))
            children = f2.IsMultiple(out var m2) ? [..m, ..m2] : [..m, f2];
        else
            children = f2.IsMultiple(out var m2) ? [f1, ..m2] : [f1, f2];

        return TFailure.Multiple(children);
    }
}