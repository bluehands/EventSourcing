using System;
using System.Reactive;
using EventSourcing.Funicular.Commands;
using EventSourcing.Funicular.Commands.Infrastructure;
using EventSourcing.Funicular.Commands.SerializablePayloads;

// ReSharper disable once CheckNamespace
namespace EventSourcing;

public static class FunicularCommandsOptionsExtensions
{
    public static EventSourcingOptionsBuilder UseFunicularCommands<TFailure, TFailurePayload, TResult>(
        this EventSourcingOptionsBuilder optionsBuilder,
        Action<FunicularCommandsOptionsBuilder<TFailure, TFailurePayload, TResult>>? funicularCommandsOptionsAction = null)
        where TFailure : IFailure<TFailure>
        where TFailurePayload : class, IFailurePayload<TFailure, TFailurePayload>
        where TResult : IResult<Unit, TFailure, TResult>
    {
        var builder = new FunicularCommandsOptionsBuilder<TFailure, TFailurePayload, TResult>(optionsBuilder);

        funicularCommandsOptionsAction?.Invoke(builder);

        return optionsBuilder;
    }
}