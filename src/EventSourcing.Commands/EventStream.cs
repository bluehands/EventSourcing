using EventSourcing.Internal;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using EventSourcing.Commands.Internal;
using FunicularSwitch.Generators;
using Microsoft.Extensions.Logging;
using AsyncLock = EventSourcing.Commands.Internal.AsyncLock;

namespace EventSourcing.Commands;

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
	public static IDisposable SubscribeCommandProcessors(this IObservable<Command> commands, GetCommandProcessor getCommandProcessor, IEventStore writeEvents, ILogger logger, WakeUp? eventPollWakeUp) =>
		commands
			.Process(getCommandProcessor)
            .SelectMany(async processingResult =>
            {
                var commandProcessed = processingResult.ToCommandProcessedEvent();
                var commandProcessedPayload = new List<EventPayload>() { commandProcessed };
                var payloads = processingResult is ProcessingResult.Processed_ p
                    ? p.ResultEvents.Concat(commandProcessedPayload).ToList()
                    : commandProcessedPayload;
                try
                {
                    await writeEvents.WriteEvents(payloads);
                    eventPollWakeUp?.ThereIsWorkToDo();
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Failed to persist events: {eventPayloads}", string.Join(", ", payloads.Select(payload => payload.ToString())));
                }

                return Unit.Default;
            })
            .Subscribe();

	public static IObservable<ProcessingResult> Process(this IObservable<Command> commands, GetCommandProcessor getCommandProcessor) =>
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

    public static CommandProcessed ToCommandProcessedEvent(this ProcessingResult r)
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

[ResultType(errorType: typeof(Failure))]
public abstract partial class OperationResult
{
    public static OperationResult<T> InvalidInput<T>(string message) => Error<T>(Failure.InvalidInput(message));
    public static OperationResult<T> Forbidden<T>(string message) => Error<T>(Failure.Forbidden(message));
    public static OperationResult<T> Conflict<T>(string message) => Error<T>(Failure.Conflict(message));
    public static OperationResult<T> NotFound<T>(string message) => Error<T>(Failure.NotFound(message));
    public static OperationResult<T> InternalError<T>(string message) => Error<T>(Failure.Internal(message));
    public static OperationResult<T> Cancelled<T>(string? message = null) => Error<T>(Failure.Cancelled(message));
}

[UnionType]
public abstract partial record Failure(string Message)
{
    public record Forbidden_(string Message) : Failure(Message);

    public record NotFound_(string Message) : Failure(Message);

    public record Conflict_(string Message) : Failure(Message);

    public record Internal_(string Message) : Failure(Message);

    public record InvalidInput_(string Message) : Failure(Message);

    public record Multiple_(IReadOnlyCollection<Failure> Failures)
        : Failure(string.Join(Environment.NewLine, Failures.Select(f => f.Message)))
    {
        public IReadOnlyCollection<Failure> Failures { get; } = Failures;
    }

    public record Cancelled_ : Failure
    {
        public Cancelled_(string? message = null) : base(message ?? "Operation cancelled")
        {
        }
    }

    public override string ToString() => $"{GetType().Name}: {Message}";
}