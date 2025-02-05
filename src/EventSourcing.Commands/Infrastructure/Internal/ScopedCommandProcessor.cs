using System;

namespace EventSourcing.Commands.Infrastructure.Internal;

public delegate ScopedCommandProcessor<TError>? GetCommandProcessor<TError>(Type commandType) where TError : notnull;

public sealed class ScopedCommandProcessor<TError>(CommandProcessor<TError> processor, IDisposable scope)
    : IDisposable where TError : notnull
{
    public CommandProcessor<TError> Processor { get; } = processor;

    public void Dispose() => scope.Dispose();
}