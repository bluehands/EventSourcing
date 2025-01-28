using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace EventSourcing.Funicular.Commands;

public interface IFailure
{
    string Message { get; }
}

public interface IFailure<TFailure> : IFailure
    where TFailure : IFailure<TFailure>
{
    static abstract TFailure Multiple(IReadOnlyCollection<TFailure> failures);

    static abstract TFailure Internal(string message);

    static abstract TFailure Cancelled(string message);

    bool IsMultiple([NotNullWhen(true)] out IReadOnlyCollection<TFailure>? failures);
}