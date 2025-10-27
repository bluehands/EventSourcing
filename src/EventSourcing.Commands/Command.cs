using System;
using Microsoft.Extensions.DependencyInjection;

namespace EventSourcing.Commands;

public abstract record Command
{
    public CommandId Id { get; } = CommandId.NewCommandId();

    public override string ToString() => $"{GetType().Name} ({Id.Id})";
}

public readonly record struct ScopedCommand(Command Command, IServiceScope ServiceScope, bool DisposeAfterProcess) : IDisposable
{
    public void Dispose()
    {
        ServiceScope.Dispose();
    }
}