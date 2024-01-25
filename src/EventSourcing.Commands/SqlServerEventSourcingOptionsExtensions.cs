using System;
using EventSourcing.Funicular.Commands.Infrastructure;

// ReSharper disable once CheckNamespace
namespace EventSourcing;

public static class SqlServerEventSourcingOptionsExtensions
{
    public static EventSourcingOptionsBuilder UseSqlServerEventStore(this EventSourcingOptionsBuilder optionsBuilder, Action<FunicularCommandsOptionsBuilder>? funicularCommandsOptionsAction = null)
    {
        var builder = new FunicularCommandsOptionsBuilder(optionsBuilder);
            
        funicularCommandsOptionsAction?.Invoke(builder);

        return optionsBuilder
            .SetDefaultEventStreamIfNotConfigured(builder);
    }

    static EventSourcingOptionsBuilder SetDefaultEventStreamIfNotConfigured(this EventSourcingOptionsBuilder optionsBuilder, FunicularCommandsOptionsBuilder builder)
    {
        return optionsBuilder;
    }
}