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
    public static IDisposable SubscribeCommandProcessors(this IObservable<Command> commands, GetCommandProcessor getCommandProcessor, Func<ScopedEventStore> getEventStore, ILogger? logger, WakeUp? eventPollWakeUp) =>
        commands
            .Process(getCommandProcessor, logger)
            .SelectMany(async processingResult =>
            {
                var commandProcessed = processingResult.ToCommandProcessedEvent();
                var payloads = processingResult is CommandResult.Processed_ p
                    ? [.. p.ResultEvents, commandProcessed]
                    : new[] { (IEventPayload)commandProcessed };

                using var eventStore = getEventStore();
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
        IEnumerable<IEventPayload> payloads,
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

    static async Task InternalWriteEvents(IEventStore eventStore, IReadOnlyCollection<IEventPayload> payloads, WakeUp? eventPollWakeUp)
    {
        await eventStore.WriteEvents(payloads).ConfigureAwait(false);
        eventPollWakeUp?.ThereIsWorkToDo();
    }

    static IObservable<CommandResult> Process(this IObservable<Command> commands,
        GetCommandProcessor getCommandProcessor, ILogger? logger) =>
        commands
            .SelectMany(async c =>
            {
                if (IsDebugEnabled(logger)) logger?.LogDebug("Executing {Command}...", c);
                var result = await CommandProcessor.Process(c, getCommandProcessor).ConfigureAwait(false);
                if (IsDebugEnabled(logger)) logger?.LogDebug("Execution finished with {CommandResult}.", result);
                return result;
            });

    static bool IsDebugEnabled(ILogger? logger) => logger?.IsEnabled(LogLevel.Debug) ?? false;

    static CommandProcessed ToCommandProcessedEvent(this CommandResult r)
    {
        var t = r.Match(
            processed: p => (
                operationResult: p.FunctionalResult.Match(
                    ok: _ => OperationResult.Ok(Unit.Default),
                    failed: failed => OperationResult.Error<Unit>(failed.Failure)
                )
                , message: (string?)p.FunctionalResult.Message
            ),
            unhandled: u => (operationResult: OperationResult.InternalError<Unit>(u.Message), null),
            faulted: f => (operationResult: OperationResult.InternalError<Unit>(f.ToString()), null),
            cancelled: c => (operationResult: OperationResult.Cancelled<Unit>(c.ToString()), null)
        );
        return new(r.CommandId, t.operationResult, t.message);
    }
}