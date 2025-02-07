using System;
using EventSourcing.Commands.Infrastructure;
using EventSourcing.Commands.SerializablePayloads;

// ReSharper disable once CheckNamespace
namespace EventSourcing;

public static class FunicularCommandsOptionsExtensions
{
    public static EventSourcingOptionsBuilder UseFunicularCommands<TError, TErrorPayload>(
        this EventSourcingOptionsBuilder optionsBuilder,
        Action<FunicularCommandsOptionsBuilder<TError, TErrorPayload>>? funicularCommandsOptionsAction = null)
        where TErrorPayload : class, IErrorPayload<TError, TErrorPayload> where TError : notnull
    {
        var builder = new FunicularCommandsOptionsBuilder<TError, TErrorPayload>(optionsBuilder);

        funicularCommandsOptionsAction?.Invoke(builder);

        return optionsBuilder;
    }
}