using System;
using System.Collections.Generic;
using FunicularSwitch.Generators;

namespace EventSourcing.Funicular.Commands;

[UnionType(CaseOrder = CaseOrder.AsDeclared)]
public abstract partial record CommandResult<TFailure>(CommandId CommandId)
{
    public sealed record Processed_(
        IReadOnlyCollection<IEventPayload> ResultEvents,
        CommandId CommandId,
        FunctionalResult<TFailure> FunctionalResult) : CommandResult<TFailure>(CommandId)
    {
        public override string ToString() => $"Command '{CommandId}', FunctionalResult: '{FunctionalResult}', Produced events count: {ResultEvents.Count}";
    }

    public sealed record Faulted_(Exception Exception, CommandId CommandId, string Message)
        : CommandResult<TFailure>(CommandId)
    {
        public override string ToString() => $"Command {CommandId} faulted: {Message} - {Exception}";
    }

    public sealed record Unhandled_(CommandId CommandId, string Message) : CommandResult<TFailure>(CommandId)
    {
        public override string ToString() => $"Command {CommandId} unhandled: {Message}";
    }

    public sealed record Cancelled_(CommandId CommandId) : CommandResult<TFailure>(CommandId)
    {
        public override string ToString() => $"Command {CommandId} cancelled";
    }
}