using System;

namespace EventSourcing.Commands.Infrastructure.Internal;

public delegate ScopedCommandProcessor<TFailure>? GetCommandProcessor<TFailure>(Type commandType) where TFailure : notnull;

public sealed class ScopedCommandProcessor<TFailure>(CommandProcessor<TFailure> processor, IDisposable scope)
    : IDisposable where TFailure : notnull
{
    public CommandProcessor<TFailure> Processor { get; } = processor;

    public void Dispose() => scope.Dispose();
}