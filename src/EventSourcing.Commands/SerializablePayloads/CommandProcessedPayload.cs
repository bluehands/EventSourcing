using System;

namespace EventSourcing.Commands.SerializablePayloads;

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
    Ok = 0,
    Failed = 1
}

public enum CommandResultUnionCases
{
    Processed = 0,
    Faulted = 1,
    Unhandled = 2,
    Cancelled = 3
}