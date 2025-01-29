using System;

namespace EventSourcing.Funicular.Commands.SerializablePayloads;

[SerializableEventPayload(EventTypes.CommandProcessed)]
public record CommandProcessedPayload<TFailurePayload>(
    Guid CommandId,
    CommandResultUnionCases CommandResult,
    FunctionalResultUnionCases? FunctionalResult,
    TFailurePayload? Failure,
    string? ResultMessage)
    where TFailurePayload : class;

public enum FunctionalResultUnionCases
{
    Ok,
    Failed
}

public enum CommandResultUnionCases
{
    Processed,
    Faulted,
    Unhandled,
    Cancelled
}