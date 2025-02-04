using System;
using FunicularSwitch.Generators;

namespace EventSourcing.Commands;

[UnionType(CaseOrder = CaseOrder.AsDeclared)]
public abstract partial record CommandResult<TFailure>(CommandId CommandId) where TFailure : notnull
{
    public sealed record Processed_(
        CommandId CommandId,
        FunctionalResult<TFailure> FunctionalResult) : CommandResult<TFailure>(CommandId)
    {
        public override string ToString() => $"Command '{CommandId}', FunctionalResult: '{FunctionalResult}'";
    }

    public sealed record Faulted_(CommandId CommandId, string Message, Exception? ErrorDetails)
        : CommandResult<TFailure>(CommandId)
    {
        public override string ToString() => $"Command {CommandId} faulted: {Message}{(ErrorDetails != null ?  $" - Details: {ErrorDetails}" : "")}";
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