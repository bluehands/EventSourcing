using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using EventSourcing.Infrastructure.Internal;
using Microsoft.Extensions.Logging;

namespace EventSourcing.Commands.Infrastructure.Internal;

public static class CommandRegistrationExtensions
{
    public static IDisposable SubscribeCommandProcessors<TFailure>(
        this IObservable<Command> commands,
        GetCommandProcessor<TFailure> getCommandProcessor,
        Func<ScopedEventStore> getEventStore,
        ILogger? logger,
        WakeUp? eventPollWakeUp) where TFailure : notnull => commands
            .Process(getCommandProcessor, logger)
            .SelectMany(async processingResult =>
            {
                var commandProcessed = new CommandProcessed<TFailure>(processingResult.result);
                IReadOnlyCollection<IEventPayload> payloads = [.. processingResult.payloads, commandProcessed];

                using var eventStore = getEventStore();
                try
                {
                    await InternalWriteEvents(eventStore, payloads, eventPollWakeUp).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    await OnEventWriteError<TFailure>(eventStore, payloads, e, commandProcessed.CommandId, eventPollWakeUp, logger).ConfigureAwait(false);
                }

                return Unit.Default;
            })
            .Subscribe();

    static async Task OnEventWriteError<TFailure>(IEventStore eventStore,
        IEnumerable<IEventPayload> payloads,
        Exception e,
        CommandId commandId,
        WakeUp? eventPollWakeUp,
        ILogger? logger) where TFailure : notnull
    {
        try
        {
            var payloadInfo = string.Join(", ", payloads.Select(payload => payload.ToString()));
            logger?.LogError(e, "Failed to persist events: {eventPayloads}. Trying to persist faulted event for command...", payloadInfo);
            var eventPayloads = new[]
            {
                new CommandProcessed<TFailure>(CommandResult<TFailure>.Faulted(commandId, $"Failed to persist events: {payloadInfo}: {e}", e)),
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

    static IObservable<(CommandResult<TFailure> result, IReadOnlyCollection<IEventPayload> payloads)> Process<TFailure>(this IObservable<Command> commands,
        GetCommandProcessor<TFailure> getCommandProcessor, ILogger? logger) where TFailure : notnull
        => commands
            .SelectMany(async c =>
            {
                if (IsDebugEnabled(logger)) logger?.LogDebug("Executing {Command}...", c);
                var result = await CommandProcessor<TFailure>.Process(c, getCommandProcessor).ConfigureAwait(false);
                if (IsDebugEnabled(logger)) logger?.LogDebug("Execution finished with {CommandResult}.", result);
                return result;
            });

    static bool IsDebugEnabled(ILogger? logger) => logger?.IsEnabled(LogLevel.Debug) ?? false;
}