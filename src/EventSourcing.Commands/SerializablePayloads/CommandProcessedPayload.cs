using System;

namespace EventSourcing.Commands.SerializablePayloads;

[SerializableEventPayload(EventTypes.CommandProcessed)]
public class CommandProcessedPayload<TErrorPayload> where TErrorPayload : class
{
    public CommandProcessedPayload() { }

    public CommandProcessedPayload(Guid commandId,
        CommandResultUnionCases commandResult,
        FunctionalResultUnionCases? functionalResult,
        TErrorPayload? error,
        string? resultMessage)
    {
        CommandId = commandId;
        CommandResult = commandResult;
        FunctionalResult = functionalResult;
        Error = error;
        ResultMessage = resultMessage;
    }

    public Guid CommandId { get; set; }
    public CommandResultUnionCases CommandResult { get; set; }
    public FunctionalResultUnionCases? FunctionalResult { get; set; }
    public TErrorPayload? Error { get; set; }
    public string? ResultMessage { get; set; }
}

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