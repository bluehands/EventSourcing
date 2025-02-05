using System;

namespace EventSourcing.Commands.SerializablePayloads;

[SerializableEventPayload(EventTypes.CommandProcessed)]
public record CommandProcessedPayload<TErrorPayload>(
    Guid CommandId,
    CommandResultUnionCases CommandResult,
    FunctionalResultUnionCases? FunctionalResult,
    TErrorPayload? Error,
    string? ResultMessage)
    where TErrorPayload : class;

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