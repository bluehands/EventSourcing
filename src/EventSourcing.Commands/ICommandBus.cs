using System;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;

namespace EventSourcing.Commands;

public interface ICommandBus
{
    Task SendCommand(Command command);
}

public static class CommandBusExtension
{
    public static Task<Event<CommandProcessed<TError>>> SendAndWaitForProcessedEvent<TError>(this ICommandBus commandBus, Command command, IObservable<Event> events) where TError : notnull => commandBus.SendAndWaitForProcessedEvent(command, events
            .OfType<Event<CommandProcessed<TError>>>());

    /// <summary>
    /// Sends a command and waits up to the supplied timeout for its processed event.
    /// A timeout ends the wait, not command execution, and does not establish the persistence outcome.
    /// </summary>
    public static Task<Event<CommandProcessed<TError>>> SendAndWaitForProcessedEvent<TError>(this ICommandBus commandBus, Command command, IObservable<Event> events, TimeSpan timeout) where TError : notnull => commandBus.SendAndWaitForProcessedEvent(command, events
            .OfType<Event<CommandProcessed<TError>>>(), timeout);

    public static Task<Event<CommandProcessed<TError>>> SendAndWaitForProcessedEvent<TError>(this ICommandBus commandBus, Command command, IObservable<Event<CommandProcessed<TError>>> commandProcessedEvents) where TError : notnull =>
        SendAndWaitForProcessedEventCore(commandBus, command, commandProcessedEvents, null);

    /// <summary>
    /// Sends a command and waits up to the supplied timeout for its processed event.
    /// A timeout ends the wait, not command execution, and does not establish the persistence outcome.
    /// </summary>
    public static Task<Event<CommandProcessed<TError>>> SendAndWaitForProcessedEvent<TError>(this ICommandBus commandBus, Command command, IObservable<Event<CommandProcessed<TError>>> commandProcessedEvents, TimeSpan timeout) where TError : notnull =>
        SendAndWaitForProcessedEventCore(commandBus, command, commandProcessedEvents, timeout);

    static async Task<Event<CommandProcessed<TError>>> SendAndWaitForProcessedEventCore<TError>(ICommandBus commandBus, Command command, IObservable<Event<CommandProcessed<TError>>> commandProcessedEvents, TimeSpan? timeout) where TError : notnull
    {
        using var deadline = timeout.HasValue ? new CancellationTokenSource(timeout.Value) : null;
        var deadlineToken = deadline?.Token ?? CancellationToken.None;

        var processed = commandProcessedEvents
            .FirstAsync(e => e.Payload.CommandId == command.Id)
            .ToTask(deadlineToken, Scheduler.Default); // Preserve scheduled task completion for sync / async mixtures.
        try
        {
            await commandBus.SendCommand(command).WaitAsync(deadlineToken).ConfigureAwait(false);
            return await processed.WaitAsync(deadlineToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (timeout.HasValue && deadline?.IsCancellationRequested == true)
        {
            throw new TimeoutException($"Command '{command.Id}' was not observed as processed within {timeout.Value}.");
        }
        finally
        {
            if (deadline != null)
            {
	            // ReSharper disable once MethodHasAsyncOverload
	            deadline.Cancel();
                try
                {
                    // Rx unsubscribes before completing its cancelled task, including concurrent timer cancellation.
                    await processed.ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // Observe cleanup completion without replacing the original send/wait outcome.
                }
            }
        }
    }
}
