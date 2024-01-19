using EventSourcing.Infrastructure;

namespace EventSourcing.Persistence.EntityFramework.Infrastructure;

class EventDescriptor : IDbEventDescriptor<Event, string>
{
    public string GetEventType(Event dbEvent) => dbEvent.EventType;

    public string GetPayload(Event dbEvent) => dbEvent.Payload;

    public long GetPosition(Event dbEvent) => dbEvent.Position;

    public DateTimeOffset GetTimestamp(Event dbEvent) => dbEvent.Timestamp;

    public Event CreateDbEvent(StreamId streamId, string eventType, string payload) => new(0, streamId.StreamType, streamId.Id, eventType, payload, DateTimeOffset.Now);
}