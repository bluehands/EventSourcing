﻿using System;

namespace EventSourcing.Funicular.Commands.JsonPayload;

[SerializableEventPayload(EventTypes.CommandProcessed)]
public record CommandProcessedPayload<TFailurePayload>(
    Guid CommandId,
    OperationResultPayload<TFailurePayload> OperationResult,
    string? ResultMessage)
    where TFailurePayload : class;