using System;

namespace EventSourcing.Funicular.Commands.Infrastructure.Internal;

public delegate ScopedCommandProcessor<TFailure>? GetCommandProcessor<TFailure>(Type commandType)
    where TFailure : IFailure<TFailure>;

public sealed class ScopedCommandProcessor<TFailure> : IDisposable
    where TFailure : IFailure<TFailure>
{
    readonly IDisposable _scope;

    public ScopedCommandProcessor(CommandProcessor<TFailure> processor, IDisposable scope)
    {
        Processor = processor;
        _scope = scope;
    }

    public CommandProcessor<TFailure> Processor { get; }

    public void Dispose() => _scope.Dispose();
}