#nullable enable

namespace PersistenceTester
{
    static partial class CommandExtensions
    {
        public static global::System.Threading.Tasks.Task<global::EventSourcing.Event<global::EventSourcing.Commands.CommandProcessed<string>>> SendAndWaitForProcessedEvent(
            this global::EventSourcing.Commands.ICommandBus commandBus, global::EventSourcing.Commands.Command command, global::System.IObservable<global::EventSourcing.Event> events)
            =>
                global::EventSourcing.Commands.CommandBusExtension.SendAndWaitForProcessedEvent<string>(commandBus, command, events);

        public static global::System.Threading.Tasks.Task<global::EventSourcing.Event<global::EventSourcing.Commands.CommandProcessed<string>>> SendAndWaitForProcessedEvent(
            this global::EventSourcing.Commands.ICommandBus commandBus, global::EventSourcing.Commands.Command command,
            global::System.IObservable<global::EventSourcing.Event<global::EventSourcing.Commands.CommandProcessed<string>>> commandProcessedEvents)
            => global::EventSourcing.Commands.CommandBusExtension.SendAndWaitForProcessedEvent(commandBus, command, commandProcessedEvents);
    }

    public abstract partial class CommandProcessor<T> : global::EventSourcing.Commands.CommandProcessor<T, string>
        where T : global::EventSourcing.Commands.Command
    {
    }

    public abstract partial class SynchronousCommandProcessor<T> : global::EventSourcing.Commands.SynchronousCommandProcessor<T, string>
        where T : global::EventSourcing.Commands.Command
    {
    }

    public static partial class ProcessingResult
    {
        public static global::EventSourcing.Commands.ProcessingResult<string> Ok(global::System.Collections.Generic.IReadOnlyCollection<global::EventSourcing.EventPayload> payloads, string? message = null) => global::EventSourcing.Commands.ProcessingResult<string>.Ok(payloads, message);
        public static global::EventSourcing.Commands.ProcessingResult<string> Ok(global::EventSourcing.EventPayload payload, string? message = null) => global::EventSourcing.Commands.ProcessingResult<string>.Ok(payload, message);
        public static global::EventSourcing.Commands.ProcessingResult<string> Ok(string message) => global::EventSourcing.Commands.ProcessingResult<string>.Ok(message);
        public static global::EventSourcing.Commands.ProcessingResult<string> Failed(global::System.Collections.Generic.IReadOnlyCollection<global::EventSourcing.EventPayload> payloads, string failure) => global::EventSourcing.Commands.ProcessingResult<string>.Failed(payloads, failure);
        public static global::EventSourcing.Commands.ProcessingResult<string> Failed(string failure) => Failed([], failure);
    }
}