#nullable enable

namespace EventSourcing.Funicular.Commands.Templates
{
    public static partial class CommandBusExtensions
    {
        public static global::System.Threading.Tasks.Task<global::EventSourcing.Event<global::EventSourcing.Funicular.Commands.CommandProcessed<FailureTypeName>>> SendAndWaitForProcessedEvent(
            this global::EventSourcing.Funicular.Commands.ICommandBus commandBus, global::EventSourcing.Funicular.Commands.Command command, global::System.IObservable<global::EventSourcing.Event> events)
            =>
                global::EventSourcing.Funicular.Commands.CommandBusExtension.SendAndWaitForProcessedEvent<FailureTypeName>(commandBus, command, events);

        public static global::System.Threading.Tasks.Task<global::EventSourcing.Event<global::EventSourcing.Funicular.Commands.CommandProcessed<FailureTypeName>>> SendAndWaitForProcessedEvent(
            this global::EventSourcing.Funicular.Commands.ICommandBus commandBus, global::EventSourcing.Funicular.Commands.Command command,
            global::System.IObservable<global::EventSourcing.Event<global::EventSourcing.Funicular.Commands.CommandProcessed<FailureTypeName>>> commandProcessedEvents)
            => global::EventSourcing.Funicular.Commands.CommandBusExtension.SendAndWaitForProcessedEvent(commandBus, command, commandProcessedEvents);
    }

    public abstract partial class CommandProcessor<T> : global::EventSourcing.Funicular.Commands.CommandProcessor<T, FailureTypeName>
        where T : global::EventSourcing.Funicular.Commands.Command
    {
    }

    public abstract partial class SynchronousCommandProcessor<T> : global::EventSourcing.Funicular.Commands.SynchronousCommandProcessor<T, FailureTypeName>
        where T : global::EventSourcing.Funicular.Commands.Command
    {
    }

    public static partial class ProcessingResult
    {
        public static global::EventSourcing.Funicular.Commands.ProcessingResult<FailureTypeName> Ok(global::System.Collections.Generic.IReadOnlyCollection<global::EventSourcing.EventPayload> payloads, string? message = null) => global::EventSourcing.Funicular.Commands.ProcessingResult<FailureTypeName>.Ok(payloads, message);
        public static global::EventSourcing.Funicular.Commands.ProcessingResult<FailureTypeName> Ok(global::EventSourcing.EventPayload payload, string? message = null) => global::EventSourcing.Funicular.Commands.ProcessingResult<FailureTypeName>.Ok(payload, message);
        public static global::EventSourcing.Funicular.Commands.ProcessingResult<FailureTypeName> Ok(string message) => global::EventSourcing.Funicular.Commands.ProcessingResult<FailureTypeName>.Ok(message);
        public static global::EventSourcing.Funicular.Commands.ProcessingResult<FailureTypeName> Failed(global::System.Collections.Generic.IReadOnlyCollection<global::EventSourcing.EventPayload> payloads, FailureTypeName failure) => global::EventSourcing.Funicular.Commands.ProcessingResult<FailureTypeName>.Failed(payloads, failure);
        public static global::EventSourcing.Funicular.Commands.ProcessingResult<FailureTypeName> Failed(FailureTypeName failure) => Failed([], failure);
    }
}