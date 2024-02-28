using System;
using System.Collections.Generic;
using FunicularSwitch.Generators;

namespace EventSourcing.Funicular.Commands;

[UnionType]
public abstract partial class CommandResult(CommandId commandId)
{
    public CommandId CommandId { get; } = commandId;

    public sealed class Processed_ : CommandResult
    {
        public IReadOnlyCollection<IEventPayload> ResultEvents { get; }

        public FunctionalResult FunctionalResult { get; }

        public Processed_(IEventPayload resultEvent, CommandId commandId, FunctionalResult functionalResult) : this(new[] { resultEvent }, commandId, functionalResult)
        {
        }

        public Processed_(IReadOnlyCollection<IEventPayload> resultEvents, CommandId commandId, FunctionalResult functionalResult) : base(commandId)
        {
            FunctionalResult = functionalResult;
            ResultEvents = resultEvents;
        }

        public override string ToString() => $"Command '{CommandId}', FunctionalResult: '{FunctionalResult}', Produced events count: {ResultEvents.Count}";
    }

    public sealed class Faulted_(Exception exception, CommandId commandId, string message) : CommandResult(commandId)
    {
        public Exception Exception { get; } = exception;
        public string Message { get; } = message;

        public override string ToString() => $"Command {CommandId} faulted: {Message} - {Exception}";
    }

    public sealed class Unhandled_(CommandId commandId, string message) : CommandResult(commandId)
    {
        public string Message { get; } = message;

        public override string ToString() => $"Command {CommandId} unhandled: {Message}";
    }

    public sealed class Cancelled_(CommandId commandId) : CommandResult(commandId)
    {
        public override string ToString() => $"Command {CommandId} cancelled";
    }
}