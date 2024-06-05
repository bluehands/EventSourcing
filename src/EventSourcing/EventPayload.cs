namespace EventSourcing;

public interface IEventPayload
{
    StreamId StreamId { get; }
    string EventType { get; }
}

public abstract record EventPayload(StreamId StreamId, string EventType) : IEventPayload;