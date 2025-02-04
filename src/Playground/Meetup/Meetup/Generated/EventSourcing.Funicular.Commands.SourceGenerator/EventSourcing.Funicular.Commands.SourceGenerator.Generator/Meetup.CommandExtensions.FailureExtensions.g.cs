#nullable enable

namespace Meetup
{
    public static partial class CommandExtensions
    {
        public static global::System.Threading.Tasks.Task<global::EventSourcing.Event<global::EventSourcing.Funicular.Commands.CommandProcessed<global::Meetup.Failure>>> SendAndWaitForProcessedEvent(
            this global::EventSourcing.Funicular.Commands.ICommandBus commandBus, global::EventSourcing.Funicular.Commands.Command command, global::System.IObservable<global::EventSourcing.Event> events)
            =>
                global::EventSourcing.Funicular.Commands.CommandBusExtension.SendAndWaitForProcessedEvent<global::Meetup.Failure>(commandBus, command, events);

        public static global::System.Threading.Tasks.Task<global::EventSourcing.Event<global::EventSourcing.Funicular.Commands.CommandProcessed<global::Meetup.Failure>>> SendAndWaitForProcessedEvent(
            this global::EventSourcing.Funicular.Commands.ICommandBus commandBus, global::EventSourcing.Funicular.Commands.Command command,
            global::System.IObservable<global::EventSourcing.Event<global::EventSourcing.Funicular.Commands.CommandProcessed<global::Meetup.Failure>>> commandProcessedEvents)
            => global::EventSourcing.Funicular.Commands.CommandBusExtension.SendAndWaitForProcessedEvent(commandBus, command, commandProcessedEvents);
    }

    public abstract partial class CommandProcessor<T> : global::EventSourcing.Funicular.Commands.CommandProcessor<T, global::Meetup.Failure>
        where T : global::EventSourcing.Funicular.Commands.Command
    {
    }

    public abstract partial class SynchronousCommandProcessor<T> : global::EventSourcing.Funicular.Commands.SynchronousCommandProcessor<T, global::Meetup.Failure>
        where T : global::EventSourcing.Funicular.Commands.Command
    {
    }

    public static partial class ProcessingResult
    {
        public static global::EventSourcing.Funicular.Commands.ProcessingResult<global::Meetup.Failure> Ok(global::System.Collections.Generic.IReadOnlyCollection<global::EventSourcing.EventPayload> payloads, string? message = null) => global::EventSourcing.Funicular.Commands.ProcessingResult<global::Meetup.Failure>.Ok(payloads, message);
        public static global::EventSourcing.Funicular.Commands.ProcessingResult<global::Meetup.Failure> Ok(global::EventSourcing.EventPayload payload, string? message = null) => global::EventSourcing.Funicular.Commands.ProcessingResult<global::Meetup.Failure>.Ok(payload, message);
        public static global::EventSourcing.Funicular.Commands.ProcessingResult<global::Meetup.Failure> Ok(string message) => global::EventSourcing.Funicular.Commands.ProcessingResult<global::Meetup.Failure>.Ok(message);
        public static global::EventSourcing.Funicular.Commands.ProcessingResult<global::Meetup.Failure> Failed(global::System.Collections.Generic.IReadOnlyCollection<global::EventSourcing.EventPayload> payloads, global::Meetup.Failure failure) => global::EventSourcing.Funicular.Commands.ProcessingResult<global::Meetup.Failure>.Failed(payloads, failure);
        public static global::EventSourcing.Funicular.Commands.ProcessingResult<global::Meetup.Failure> Failed(global::Meetup.Failure failure) => Failed([], failure);
    }
}