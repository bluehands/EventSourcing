using System.Reactive;

namespace EventSourcing.Funicular.Commands;

public static class StreamIds
{
    public static readonly StreamId Command = new("Command", "Command");
}

public static class EventTypes
{
    public const string CommandProcessed = "CommandProcessed";
}

public record CommandProcessed(CommandId CommandId, OperationResult<Unit> OperationResult, string? ResultMessage)
    : EventPayload(StreamIds.Command, EventTypes.CommandProcessed)
{
    public override string ToString() => $"{CommandId} processed with result {OperationResult}: {ResultMessage}";
}