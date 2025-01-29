using System;

namespace EventSourcing.Funicular.Commands.SerializablePayloads;

[SerializableEventPayload(EventTypes.CommandProcessed)]
public record CommandProcessedPayload<TFailurePayload>(
    Guid CommandId,
    ResultPayload<TFailurePayload> Result,
    string? ResultMessage)
    where TFailurePayload : class;