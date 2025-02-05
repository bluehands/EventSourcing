namespace EventSourcing.Commands;

public static class StreamIds
{
    public static readonly StreamId Command = new("Command", "Command");
}

public static class EventTypes
{
    public const string CommandProcessed = "CommandProcessed";
}

public record CommandProcessed<TError>(CommandResult<TError> CommandResult)
    : EventPayload(StreamIds.Command, EventTypes.CommandProcessed) where TError : notnull
{
    public override string ToString() => $"Processed: {CommandResult}";
    public CommandId CommandId => CommandResult.CommandId;
}