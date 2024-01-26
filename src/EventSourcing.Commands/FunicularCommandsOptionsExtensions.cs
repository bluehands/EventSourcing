using System;
using EventSourcing.Funicular.Commands.Infrastructure;

// ReSharper disable once CheckNamespace
namespace EventSourcing;

public static class FunicularCommandsOptionsExtensions
{
    public static EventSourcingOptionsBuilder UseFunicularCommands(this EventSourcingOptionsBuilder optionsBuilder, Action<FunicularCommandsOptionsBuilder>? funicularCommandsOptionsAction = null)
    {
        var builder = new FunicularCommandsOptionsBuilder(optionsBuilder);

        funicularCommandsOptionsAction?.Invoke(builder);

        return optionsBuilder;
    }
}