using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using EventSourcing.Infrastructure.Internal;
using Microsoft.Extensions.Logging;

namespace EventSourcing.Funicular.Commands.Infrastructure.Internal;

public static class CommandRegistrationExtensions
{
    public static IDisposable SubscribeCommandProcessors<TFailure, TResult>(
        this IObservable<Command> commands,
        GetCommandProcessor<TFailure> getCommandProcessor,
        Func<ScopedEventStore> getEventStore,
        ILogger? logger,
        WakeUp? eventPollWakeUp)
        where TFailure : IFailure<TFailure>
        where TResult : IResult<Unit, TFailure, TResult>
        => commands
            .Process(getCommandProcessor, logger)
            .SelectMany(async processingResult =>
            {
                var commandProcessed = processingResult.ToCommandProcessedEvent<TFailure, TResult>();
                var payloads = processingResult is CommandResult<TFailure>.Processed_ p
                    ? [.. p.ResultEvents, commandProcessed]
                    : new[] { (IEventPayload)commandProcessed };

                using var eventStore = getEventStore();
                try
                {
                    await InternalWriteEvents(eventStore, payloads, eventPollWakeUp).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    await OnEventWriteError<TFailure, TResult>(eventStore, payloads, e, processingResult, eventPollWakeUp, logger).ConfigureAwait(false);
                }

                return Unit.Default;
            })
            .Subscribe();

    static async Task OnEventWriteError<TFailure, TResult>(IEventStore eventStore,
        IEnumerable<IEventPayload> payloads,
        Exception e,
        CommandResult<TFailure> commandResult,
        WakeUp? eventPollWakeUp,
        ILogger? logger)
        where TFailure : IFailure<TFailure>
        where TResult : IResult<Unit, TFailure, TResult>
    {
        try
        {
            var payloadInfo = string.Join(", ", payloads.Select(payload => payload.ToString()));
            logger?.LogError(e, "Failed to persist events: {eventPayloads}. Trying to persist faulted event for command...", payloadInfo);
            var eventPayloads = new[]
            {
                new CommandProcessed<TFailure, TResult>(commandResult.CommandId, TResult.Error(TFailure.Internal($"Failed to persist events: {payloadInfo}: {e}")), "Event write error")
            };
            await InternalWriteEvents(eventStore, eventPayloads, eventPollWakeUp).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to persist write error event. Event store might be unavailable.");
        }
    }

    static async Task InternalWriteEvents(IEventStore eventStore, IReadOnlyCollection<IEventPayload> payloads, WakeUp? eventPollWakeUp)
    {
        await eventStore.WriteEvents(payloads).ConfigureAwait(false);
        eventPollWakeUp?.ThereIsWorkToDo();
    }

    static IObservable<CommandResult<TFailure>> Process<TFailure>(this IObservable<Command> commands,
        GetCommandProcessor<TFailure> getCommandProcessor, ILogger? logger)
        where TFailure : IFailure<TFailure>
        => commands
            .SelectMany(async c =>
            {
                if (IsDebugEnabled(logger)) logger?.LogDebug("Executing {Command}...", c);
                var result = await CommandProcessor<TFailure>.Process(c, getCommandProcessor).ConfigureAwait(false);
                if (IsDebugEnabled(logger)) logger?.LogDebug("Execution finished with {CommandResult}.", result);
                return result;
            });

    static bool IsDebugEnabled(ILogger? logger) => logger?.IsEnabled(LogLevel.Debug) ?? false;

    static CommandProcessed<TFailure, TResult> ToCommandProcessedEvent<TFailure, TResult>(this CommandResult<TFailure> r)
        where TFailure : IFailure<TFailure>
        where TResult : IResult<Unit, TFailure, TResult>
    {
        var t = r.Match(
            processed: p => (result: p.FunctionalResult.Match(
                    ok: _ => TResult.Ok(Unit.Default),
                    failed: error => TResult.Error(error.Failure)
                )
                , message: (string?)p.FunctionalResult.Match(ok => ok.ResultMessage, error => error.Failure.Message)
            ),
            unhandled: u => (result: TResult.Error(TFailure.Internal(u.Message)), null),
            faulted: f => (result: TResult.Error(TFailure.Internal(f.ToString())), null),
            cancelled: c => (result: TResult.Error(TFailure.Cancelled(c.ToString())), null)
        );
        return new(r.CommandId, t.result, t.message);
    }
}