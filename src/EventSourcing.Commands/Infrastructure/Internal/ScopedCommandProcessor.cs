using System;

namespace EventSourcing.Funicular.Commands.Infrastructure.Internal;

public delegate ScopedCommandProcessor<TFailure>? GetCommandProcessor<TFailure>(Type commandType)
    where TFailure : IFailure<TFailure>;

public sealed class ScopedCommandProcessor<TFailure>(CommandProcessor<TFailure> processor, IDisposable scope)
    : IDisposable
    where TFailure : IFailure<TFailure>
{
    public CommandProcessor<TFailure> Processor { get; } = processor;

    public void Dispose() => scope.Dispose();
}