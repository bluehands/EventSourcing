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
        FunctionalResult<TFailure> FunctionalResult) : CommandResult<TFailure>(CommandId);

    public sealed record Faulted_(Exception Exception, CommandId CommandId, string Message) : CommandResult<TFailure>(CommandId);

    public sealed record Unhandled_(CommandId CommandId, string Message) : CommandResult<TFailure>(CommandId);

    public sealed record Cancelled_(CommandId CommandId) : CommandResult<TFailure>(CommandId);
}