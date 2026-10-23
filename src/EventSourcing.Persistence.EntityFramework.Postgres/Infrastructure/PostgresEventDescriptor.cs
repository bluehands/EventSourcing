using EventSourcing.Infrastructure;

namespace EventSourcing.Persistence.EntityFramework.Postgres.Infrastructure;

internal sealed class PostgresEventDescriptor : IDbEventDescriptor<Event, string>
{
    public long GetPosition(Event dbEvent) => dbEvent.Position;
    public StreamId GetStreamId(Event dbEvent) => new(dbEvent.StreamType, dbEvent.StreamId);
    public string GetEventType(Event dbEvent) => dbEvent.EventType;
    public string GetPayload(Event dbEvent) => dbEvent.Payload;
    public DateTimeOffset GetTimestamp(Event dbEvent) => dbEvent.Timestamp;
    public Event CreateDbEvent(StreamId streamId, string eventType, string payload) =>
        new(0, streamId.StreamType, streamId.Id, eventType, payload, DateTimeOffset.UtcNow);
}
