﻿#nullable enable

namespace EventSourcing.Commands.Templates
{
    public static partial class CommandBusExtensions
    {
        public static global::System.Threading.Tasks.Task<global::EventSourcing.Event<global::EventSourcing.Commands.CommandProcessed<ErrorTypeName>>> SendAndWaitForProcessedEvent(
            this global::EventSourcing.Commands.ICommandBus commandBus, global::EventSourcing.Commands.Command command, global::System.IObservable<global::EventSourcing.Event> events)
            =>
                global::EventSourcing.Commands.CommandBusExtension.SendAndWaitForProcessedEvent<ErrorTypeName>(commandBus, command, events);

        public static global::System.Threading.Tasks.Task<global::EventSourcing.Event<global::EventSourcing.Commands.CommandProcessed<ErrorTypeName>>> SendAndWaitForProcessedEvent(
            this global::EventSourcing.Commands.ICommandBus commandBus, global::EventSourcing.Commands.Command command,
            global::System.IObservable<global::EventSourcing.Event<global::EventSourcing.Commands.CommandProcessed<ErrorTypeName>>> commandProcessedEvents)
            => global::EventSourcing.Commands.CommandBusExtension.SendAndWaitForProcessedEvent(commandBus, command, commandProcessedEvents);
    }

    public abstract partial class CommandProcessor<T> : global::EventSourcing.Commands.CommandProcessor<T, ErrorTypeName>
        where T : global::EventSourcing.Commands.Command
    {
    }

    public abstract partial class SynchronousCommandProcessor<T> : global::EventSourcing.Commands.SynchronousCommandProcessor<T, ErrorTypeName>
        where T : global::EventSourcing.Commands.Command
    {
    }

    public static partial class ProcessingResult
    {
        public static global::EventSourcing.Commands.ProcessingResult<ErrorTypeName> Ok(global::System.Collections.Generic.IReadOnlyCollection<global::EventSourcing.EventPayload> payloads, string? message = null) => global::EventSourcing.Commands.ProcessingResult<ErrorTypeName>.Ok(payloads, message);
        public static global::EventSourcing.Commands.ProcessingResult<ErrorTypeName> Ok(global::EventSourcing.EventPayload payload, string? message = null) => global::EventSourcing.Commands.ProcessingResult<ErrorTypeName>.Ok(payload, message);
        public static global::EventSourcing.Commands.ProcessingResult<ErrorTypeName> Ok(string message) => global::EventSourcing.Commands.ProcessingResult<ErrorTypeName>.Ok(message);
        public static global::EventSourcing.Commands.ProcessingResult<ErrorTypeName> Failed(global::System.Collections.Generic.IReadOnlyCollection<global::EventSourcing.EventPayload> payloads, ErrorTypeName error) => global::EventSourcing.Commands.ProcessingResult<ErrorTypeName>.Failed(payloads, error);
        public static global::EventSourcing.Commands.ProcessingResult<ErrorTypeName> Failed(ErrorTypeName error) => Failed([], error);
    }
}