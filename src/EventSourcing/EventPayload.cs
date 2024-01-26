namespace EventSourcing;

public abstract record EventPayload(StreamId StreamId, string EventType);