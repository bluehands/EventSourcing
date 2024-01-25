﻿using System;
using System.Linq;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using EventSourcing.Internal;
using Microsoft.Extensions.Logging;
using AsyncLock = EventSourcing.Funicular.Commands.Infrastructure.Internal.AsyncLock;
using EventSourcing.Funicular.Commands.Infrastructure.Internal;

namespace EventSourcing.Funicular.Commands;

public sealed class CommandStream : IObservable<Command>, IDisposable
{
    readonly AsyncLock _lock = new();
    readonly Subject<Command> _innerStream;
    readonly IObservable<Command> _commands;

    public CommandStream()
    {
        _innerStream = new();
        _commands = _innerStream.Publish().RefCount();
    }

    public IDisposable Subscribe(IObserver<Command> observer) => _commands.Subscribe(observer);

    public async Task SendCommand(Command command) => await _lock.ExecuteGuarded(() => _innerStream.OnNext(command)).ConfigureAwait(false);

    public Task SendCommands(IEnumerable<Command> commands) => Task.WhenAll(commands.Select(SendCommand));

    public void Dispose()
    {
        _lock.Dispose();
        _innerStream.Dispose();
    }
}

public static class CommandStreamExtension
{
    public static IDisposable SubscribeCommandProcessors(this IObservable<Command> commands, GetCommandProcessor getCommandProcessor, IEventStore eventStore, ILogger? logger, WakeUp? eventPollWakeUp) =>
        commands
            .Process(getCommandProcessor)
            .SelectMany(async processingResult =>
            {
                var commandProcessed = processingResult.ToCommandProcessedEvent();
                var commandProcessedPayload = new List<EventPayload>() { commandProcessed };
                var payloads = processingResult is CommandResult.Processed_ p
                    ? p.ResultEvents.Concat(commandProcessedPayload).ToList()
                    : commandProcessedPayload;
                try
                {
                    await InternalWriteEvents(eventStore, payloads, eventPollWakeUp).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    await OnEventWriteError(eventStore, payloads, e, processingResult, eventPollWakeUp, logger).ConfigureAwait(false);
                }

                return Unit.Default;
            })
            .Subscribe();

    static async Task OnEventWriteError(IEventStore eventStore,
        IEnumerable<EventPayload> payloads,
        Exception e,
        CommandResult commandResult,
        WakeUp? eventPollWakeUp,
        ILogger? logger)
    {
        try
        {
            var payloadInfo = string.Join(", ", payloads.Select(payload => payload.ToString()));
            logger?.LogError(e, "Failed to persist events: {eventPayloads}. Trying to persist faulted event for command...", payloadInfo);
            var eventPayloads = new[]
            {
                new CommandProcessed(commandResult.CommandId, OperationResult.InternalError<Unit>($"Failed to persist events: {payloadInfo}: {e}"), "Event write error")
            };
            await InternalWriteEvents(eventStore, eventPayloads, eventPollWakeUp).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to persist write error event. Event store might be unavailable.");
        }
    }

    static async Task InternalWriteEvents(IEventStore eventStore, IReadOnlyCollection<EventPayload> payloads, WakeUp? eventPollWakeUp)
    {
        await eventStore.WriteEvents(payloads).ConfigureAwait(false);
        eventPollWakeUp?.ThereIsWorkToDo();
    }

    public static IObservable<CommandResult> Process(this IObservable<Command> commands, GetCommandProcessor getCommandProcessor) =>
        commands
            .SelectMany(c => CommandProcessor.Process(c, getCommandProcessor));

    public static async Task<OperationResult<Unit>> SendCommandAndWaitUntilApplied(this CommandStream commandStream,
        Command command, IObservable<CommandProcessed> commandProcessedEvents)
    {
        var processed = commandProcessedEvents
            .FirstAsync(c => c.CommandId == command.Id)
            .ToTask(CancellationToken.None, Scheduler.Default); //this is needed if we might be called from sync / async mixtures (https://blog.stephencleary.com/2012/12/dont-block-in-asynchronous-code.html)
        await commandStream.SendCommand(command).ConfigureAwait(false);

        return (await processed.ConfigureAwait(false)).OperationResult;
    }

    public static CommandProcessed ToCommandProcessedEvent(this CommandResult r)
    {
        var operationResult = r.Match(
            processed: p => p.FunctionalResult.Match(ok: _ => OperationResult.Ok(Unit.Default), failed: failed => OperationResult.Error<Unit>(failed.Failure)),
            unhandled: u => OperationResult.InternalError<Unit>(u.Message),
            faulted: f => OperationResult.InternalError<Unit>($"Command execution failed with exception: {f.Exception}"),
            cancelled: c => OperationResult.Cancelled<Unit>(c.ToString()));
        return new(r.CommandId, operationResult, r.Message);
    }
}

public static partial class StreamIds
{
    public static readonly StreamId Command = new("Command", "Command");
}

public static partial class EventTypes
{
    public const string CommandProcessed = "CommandProcessed";
}

public record CommandProcessed(CommandId CommandId, OperationResult<Unit> OperationResult, string? ResultMessage)
    : EventPayload(StreamIds.Command, EventTypes.CommandProcessed)
{
    public override string ToString() => $"{CommandId} processed with result {OperationResult}: {ResultMessage}";
}