#nullable enable

namespace Meetup
{
    public static partial class CommandExtensions
    {
        public static global::System.Threading.Tasks.Task<global::EventSourcing.Event<global::EventSourcing.Commands.CommandProcessed<global::Meetup.Error>>> SendAndWaitForProcessedEvent(
            this global::EventSourcing.Commands.ICommandBus commandBus, global::EventSourcing.Commands.Command command, global::System.IObservable<global::EventSourcing.Event> events)
            =>
                global::EventSourcing.Commands.CommandBusExtension.SendAndWaitForProcessedEvent<global::Meetup.Error>(commandBus, command, events);

        public static global::System.Threading.Tasks.Task<global::EventSourcing.Event<global::EventSourcing.Commands.CommandProcessed<global::Meetup.Error>>> SendAndWaitForProcessedEvent(
            this global::EventSourcing.Commands.ICommandBus commandBus, global::EventSourcing.Commands.Command command,
            global::System.IObservable<global::EventSourcing.Event<global::EventSourcing.Commands.CommandProcessed<global::Meetup.Error>>> commandProcessedEvents)
            => global::EventSourcing.Commands.CommandBusExtension.SendAndWaitForProcessedEvent(commandBus, command, commandProcessedEvents);
    }

    public abstract partial class CommandProcessor<T> : global::EventSourcing.Commands.CommandProcessor<T, global::Meetup.Error>
        where T : global::EventSourcing.Commands.Command
    {
    }

    public abstract partial class SynchronousCommandProcessor<T> : global::EventSourcing.Commands.SynchronousCommandProcessor<T, global::Meetup.Error>
        where T : global::EventSourcing.Commands.Command
    {
    }

    public static partial class ProcessingResult
    {
        public static global::EventSourcing.Commands.ProcessingResult<global::Meetup.Error> Ok(global::System.Collections.Generic.IReadOnlyCollection<global::EventSourcing.EventPayload> payloads, string? message = null) => global::EventSourcing.Commands.ProcessingResult<global::Meetup.Error>.Ok(payloads, message);
        public static global::EventSourcing.Commands.ProcessingResult<global::Meetup.Error> Ok(global::EventSourcing.EventPayload payload, string? message = null) => global::EventSourcing.Commands.ProcessingResult<global::Meetup.Error>.Ok(payload, message);
        public static global::EventSourcing.Commands.ProcessingResult<global::Meetup.Error> Ok(string message) => global::EventSourcing.Commands.ProcessingResult<global::Meetup.Error>.Ok(message);
        public static global::EventSourcing.Commands.ProcessingResult<global::Meetup.Error> Failed(global::System.Collections.Generic.IReadOnlyCollection<global::EventSourcing.EventPayload> payloads, global::Meetup.Error error) => global::EventSourcing.Commands.ProcessingResult<global::Meetup.Error>.Failed(payloads, error);
        public static global::EventSourcing.Commands.ProcessingResult<global::Meetup.Error> Failed(global::Meetup.Error error) => Failed([], error);
    }
}