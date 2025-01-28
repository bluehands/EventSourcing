using System;
using System.Reactive;
using EventSourcing.Funicular.Commands;
using EventSourcing.Funicular.Commands.Infrastructure;
using EventSourcing.Funicular.Commands.JsonPayload;

// ReSharper disable once CheckNamespace
namespace EventSourcing;

public static class FunicularCommandsOptionsExtensions
{
    public static EventSourcingOptionsBuilder UseFunicularCommands<TFailure, TFailurePayload, TOperationResult>(
        this EventSourcingOptionsBuilder optionsBuilder,
        Action<FunicularCommandsOptionsBuilder<TFailure, TFailurePayload, TOperationResult>>? funicularCommandsOptionsAction = null)
        where TFailure : IFailure<TFailure>
        where TFailurePayload : class, IFailurePayload<TFailure, TFailurePayload>
        where TOperationResult : IResult<Unit, TFailure, TOperationResult>
    {
        var builder = new FunicularCommandsOptionsBuilder<TFailure, TFailurePayload, TOperationResult>(optionsBuilder);

        funicularCommandsOptionsAction?.Invoke(builder);

        return optionsBuilder;
    }
}