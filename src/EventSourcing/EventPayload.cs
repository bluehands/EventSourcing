namespace EventSourcing;

public interface IEventPayload
{
    StreamId StreamId { get; init; }
    string EventType { get; init; }
}

public abstract record EventPayload(StreamId StreamId, string EventType) : IEventPayload;