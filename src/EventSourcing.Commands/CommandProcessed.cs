namespace EventSourcing.Commands;

public static class StreamIds
{
    public static readonly StreamId Command = new("Command", "Command");
}

public static class EventTypes
{
    public const string CommandProcessed = "CommandProcessed";
}

/// <summary>
/// Technical marker appended last for every processed command. It exists only so the framework can return the
/// command result to a caller waiting via <c>SendCommandAndWaitUntilApplied</c>.
/// </summary>
/// <remarks>
/// Carries no domain state and may be deleted after a retention period. Application code, queries, reactions and
/// projections must not read, subscribe to or query these events to determine what a command did. The result only
/// says whether the command succeeded or failed; ids, statuses and other outcomes must be read from the affected
/// projections.
/// </remarks>
public record CommandProcessed<TError>(CommandResult<TError> CommandResult)
    : EventPayload(StreamIds.Command, EventTypes.CommandProcessed) where TError : notnull
{
    public override string ToString() => $"Processed: {CommandResult}";
    public CommandId CommandId => CommandResult.CommandId;
}