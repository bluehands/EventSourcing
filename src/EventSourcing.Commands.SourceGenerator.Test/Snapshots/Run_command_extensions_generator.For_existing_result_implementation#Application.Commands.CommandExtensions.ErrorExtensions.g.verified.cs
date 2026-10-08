//HintName: Application.Commands.CommandExtensions.ErrorExtensions.g.cs
#nullable enable

namespace Application.Commands
{
    public static partial class CommandExtensions
    {
        public static global::System.Threading.Tasks.Task<global::EventSourcing.Event<global::EventSourcing.Commands.CommandProcessed<global::Application.Errors.Error>>> SendAndWaitForProcessedEvent(
            this global::EventSourcing.Commands.ICommandBus commandBus, global::EventSourcing.Commands.Command command, global::System.IObservable<global::EventSourcing.Event> events)
            =>
                global::EventSourcing.Commands.CommandBusExtension.SendAndWaitForProcessedEvent<global::Application.Errors.Error>(commandBus, command, events);

        public static global::System.Threading.Tasks.Task<global::EventSourcing.Event<global::EventSourcing.Commands.CommandProcessed<global::Application.Errors.Error>>> SendAndWaitForProcessedEvent(
            this global::EventSourcing.Commands.ICommandBus commandBus, global::EventSourcing.Commands.Command command,
            global::System.IObservable<global::EventSourcing.Event<global::EventSourcing.Commands.CommandProcessed<global::Application.Errors.Error>>> commandProcessedEvents)
            => global::EventSourcing.Commands.CommandBusExtension.SendAndWaitForProcessedEvent(commandBus, command, commandProcessedEvents);

        public static global::System.Threading.Tasks.Task<global::EventSourcing.Event<global::EventSourcing.Commands.CommandProcessed<global::Application.Errors.Error>>> SendAndWaitForProcessedEvent(
            this global::EventSourcing.Commands.ICommandBus commandBus, global::EventSourcing.Commands.Command command,
            global::System.IObservable<global::EventSourcing.Event> events, global::System.TimeSpan timeout)
            => global::EventSourcing.Commands.CommandBusExtension.SendAndWaitForProcessedEvent<global::Application.Errors.Error>(commandBus, command, events, timeout);

        public static global::System.Threading.Tasks.Task<global::EventSourcing.Event<global::EventSourcing.Commands.CommandProcessed<global::Application.Errors.Error>>> SendAndWaitForProcessedEvent(
            this global::EventSourcing.Commands.ICommandBus commandBus, global::EventSourcing.Commands.Command command,
            global::System.IObservable<global::EventSourcing.Event<global::EventSourcing.Commands.CommandProcessed<global::Application.Errors.Error>>> commandProcessedEvents, global::System.TimeSpan timeout)
            => global::EventSourcing.Commands.CommandBusExtension.SendAndWaitForProcessedEvent(commandBus, command, commandProcessedEvents, timeout);
    }

    public abstract partial class CommandProcessor<T> : global::EventSourcing.Commands.CommandProcessor<T, global::Application.Errors.Error>
        where T : global::EventSourcing.Commands.Command
    {
    }

    public abstract partial class SynchronousCommandProcessor<T> : global::EventSourcing.Commands.SynchronousCommandProcessor<T, global::Application.Errors.Error>
        where T : global::EventSourcing.Commands.Command
    {
    }

    public static partial class ProcessingResult
    {
        public static global::EventSourcing.Commands.ProcessingResult<global::Application.Errors.Error> Ok(global::System.Collections.Generic.IReadOnlyCollection<global::EventSourcing.EventPayload> payloads, string? message = null) => global::EventSourcing.Commands.ProcessingResult<global::Application.Errors.Error>.Ok(payloads, message);
        public static global::EventSourcing.Commands.ProcessingResult<global::Application.Errors.Error> Ok(global::EventSourcing.EventPayload payload, string? message = null) => global::EventSourcing.Commands.ProcessingResult<global::Application.Errors.Error>.Ok(payload, message);
        public static global::EventSourcing.Commands.ProcessingResult<global::Application.Errors.Error> Ok(string message) => global::EventSourcing.Commands.ProcessingResult<global::Application.Errors.Error>.Ok(message);
        public static global::EventSourcing.Commands.ProcessingResult<global::Application.Errors.Error> Failed(global::System.Collections.Generic.IReadOnlyCollection<global::EventSourcing.EventPayload> payloads, global::Application.Errors.Error error) => global::EventSourcing.Commands.ProcessingResult<global::Application.Errors.Error>.Failed(payloads, error);
        public static global::EventSourcing.Commands.ProcessingResult<global::Application.Errors.Error> Failed(global::Application.Errors.Error error) => Failed([], error);
    }
}
