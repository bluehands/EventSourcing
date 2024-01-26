using System;

namespace EventSourcing.Funicular.Commands.Infrastructure.Internal;

public delegate ScopedCommandProcessor? GetCommandProcessor(Type commandType);

public sealed class ScopedCommandProcessor : IDisposable
{
    readonly IDisposable _scope;

    public ScopedCommandProcessor(CommandProcessor processor, IDisposable scope)
    {
        Processor = processor;
        _scope = scope;
    }

    public CommandProcessor Processor { get; }

    public void Dispose() => _scope.Dispose();
}