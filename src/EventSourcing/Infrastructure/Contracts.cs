using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EventSourcing.Infrastructure;

public interface IEventReader<out TDbEvent>
{
    IAsyncEnumerable<TDbEvent> ReadEvents(StreamId streamId, long? fromPositionInclusive);

    IAsyncEnumerable<TDbEvent> ReadEvents(long? fromPositionInclusive);
}

public interface IEventWriter<in TDbEvent>
{
    Task WriteEvents(IEnumerable<TDbEvent> payloads);
}

public interface IEventMapper<TDbEvent>
{
    IEnumerable<TDbEvent> MapToDbEvents(IEnumerable<IEventPayload> payloads);
    IAsyncEnumerable<Event> MapFromDbEvents(IAsyncEnumerable<TDbEvent> dbEvents);
}

public interface IEventSerializer<TSerialized>
{
    public TSerialized Serialize(object serializablePayload);
    public object Deserialize(Type serializablePayloadType, TSerialized serializedPayload);
}

public interface IDbEventDescriptor<TDbEvent, TSerializedPayload>
{
    long GetPosition(TDbEvent dbEvent);
    StreamId GetStreamId(TDbEvent dbEvent);
    string GetEventType(TDbEvent dbEvent);
    TSerializedPayload GetPayload(TDbEvent dbEvent);
    DateTimeOffset GetTimestamp(TDbEvent dbEvent);

    TDbEvent CreateDbEvent(StreamId streamId, string eventType, TSerializedPayload payload);
}