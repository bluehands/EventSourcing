using System;
using EventSourcing.Funicular.Commands;
using EventSourcing.Funicular.Commands.Infrastructure;
using EventSourcing.Funicular.Commands.SerializablePayloads;

// ReSharper disable once CheckNamespace
namespace EventSourcing;

public static class FunicularCommandsOptionsExtensions
{
    public static EventSourcingOptionsBuilder UseFunicularCommands<TFailure, TFailurePayload>(
        this EventSourcingOptionsBuilder optionsBuilder,
        Action<FunicularCommandsOptionsBuilder<TFailure, TFailurePayload>>? funicularCommandsOptionsAction = null)
        where TFailure : IFailure<TFailure>
        where TFailurePayload : class, IFailurePayload<TFailure, TFailurePayload>
    {
        var builder = new FunicularCommandsOptionsBuilder<TFailure, TFailurePayload>(optionsBuilder);

        funicularCommandsOptionsAction?.Invoke(builder);

        return optionsBuilder;
    }
}