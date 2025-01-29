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

public record CommandProcessed<TFailure, TResult>(CommandId CommandId, TResult Result, string? ResultMessage)
    : EventPayload(StreamIds.Command, EventTypes.CommandProcessed)
    where TFailure : IFailure<TFailure>
    where TResult : IResult<Unit, TFailure>
{
    public override string ToString() => $"{CommandId} processed with result {Result}: {ResultMessage}";
}