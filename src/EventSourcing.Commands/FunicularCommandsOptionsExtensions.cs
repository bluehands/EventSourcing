using System;
using EventSourcing.Commands.Infrastructure;
using EventSourcing.Commands.SerializablePayloads;

// ReSharper disable once CheckNamespace
namespace EventSourcing;

public static class FunicularCommandsOptionsExtensions
{
    public static EventSourcingOptionsBuilder UseFunicularCommands<TFailure, TFailurePayload>(
        this EventSourcingOptionsBuilder optionsBuilder,
        Action<FunicularCommandsOptionsBuilder<TFailure, TFailurePayload>>? funicularCommandsOptionsAction = null)
        where TFailurePayload : class, IFailurePayload<TFailure, TFailurePayload> where TFailure : notnull
    {
        var builder = new FunicularCommandsOptionsBuilder<TFailure, TFailurePayload>(optionsBuilder);

        funicularCommandsOptionsAction?.Invoke(builder);

        return optionsBuilder;
    }
}