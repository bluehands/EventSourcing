using System;
using System.Collections.Generic;
using FunicularSwitch.Generators;

namespace EventSourcing.Funicular.Commands;

[UnionType]
public abstract partial class CommandResult(CommandId commandId, string? message)
{
    public CommandId CommandId { get; } = commandId;
    public string? Message { get; } = message;

    public sealed class Processed_ : CommandResult
    {
        public IReadOnlyCollection<EventPayload> ResultEvents { get; }

        public FunctionalResult FunctionalResult { get; }

        public Processed_(EventPayload resultEvent, CommandId commandId, FunctionalResult functionalResult) : this(new[] { resultEvent }, commandId, functionalResult)
        {
        }

        public Processed_(IReadOnlyCollection<EventPayload> resultEvents, CommandId commandId, FunctionalResult functionalResult) : base(commandId, functionalResult.Message)
        {
            FunctionalResult = functionalResult;
            ResultEvents = resultEvents;
        }

        public override string ToString() => $"Command {CommandId} processed with result {FunctionalResult}, Produced events count: {ResultEvents.Count}";
    }

    public sealed class Faulted_(Exception exception, CommandId commandId, string message) : CommandResult(commandId, message)
    {
        public Exception Exception { get; } = exception;

        public override string ToString() => $"Command {CommandId} faulted: {Message} - {Exception}";
    }

    public sealed class Unhandled_(CommandId commandId, string message) : CommandResult(commandId, message)
    {
        public new string Message { get; } = message;

        public override string ToString() => $"Command {CommandId} unhandled: {Message}";
    }

    public sealed class Cancelled_(CommandId commandId) : CommandResult(commandId, $"Command {commandId} cancelled")
    {
        public override string ToString() => $"Command {CommandId} cancelled";
    }
}