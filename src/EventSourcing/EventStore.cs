using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EventSourcing.Infrastructure;
using EventSourcing.Infrastructure.Internal;

namespace EventSourcing;

public class EventStore<TDbEvent, TSerializedPayload>(
    IEventReader<TDbEvent> eventReader,
    IEventWriter<TDbEvent> eventWriter,
    IDbEventDescriptor<TDbEvent, TSerializedPayload> eventDescriptor,
    IEventSerializer<TSerializedPayload> payloadSerializer,
    EventPayloadMappers payloadMappers,
    EventFactory eventFactory,
    ICorruptedEventHandler? corruptedEventHandler = null)
    : IEventStore, IEventMapper<TDbEvent>
{
    public IAsyncEnumerable<Event> ReadEvents(long? fromPositionInclusive)
    {
        var dbEvents = eventReader.ReadEvents(fromPositionInclusive);
        return MapFromDbEvents(dbEvents);
    }

    public IAsyncEnumerable<Event> ReadEvents(StreamId streamId, long? fromPositionInclusive)
    {
        var dbEvents = eventReader.ReadEvents(streamId, fromPositionInclusive);
        return MapFromDbEvents(dbEvents);
    }

    public Task WriteEvents(IReadOnlyCollection<IEventPayload> payloads)
    {
        var events = MapToDbEvents(payloads);
        return eventWriter.WriteEvents(events);
    }

    public IEnumerable<TDbEvent> MapToDbEvents(IEnumerable<IEventPayload> payloads) =>
        payloads.Select(payload =>
        {
            var serializablePayload = payloadMappers.MapToSerializablePayload(payload);
            var serializedPayload = payloadSerializer.Serialize(serializablePayload);
            return eventDescriptor.CreateDbEvent(payload.StreamId, payload.EventType, serializedPayload);
        });

    public async IAsyncEnumerable<Event> MapFromDbEvents(IAsyncEnumerable<TDbEvent> dbEvents)
    {
        await foreach (var dbEvent in dbEvents)
        {
            IEventPayload eventPayload;

            var eventType = eventDescriptor.GetEventType(dbEvent);
            var streamId = eventDescriptor.GetStreamId(dbEvent);
            var serializedPayload = eventDescriptor.GetPayload(dbEvent)!;
            var eventPosition = eventDescriptor.GetPosition(dbEvent);
            var timestamp = eventDescriptor.GetTimestamp(dbEvent);

            try
            {
                eventPayload = payloadMappers.MapFromSerializedPayload(streamId, eventType, serializedPayload, payloadSerializer != null
                    ? (t, s) => payloadSerializer.Deserialize(t, (TSerializedPayload)s)
                    : null);
            }
            catch (Exception e)
            {
                // permanent deserialize / mapping error. 
                var errorPayload = corruptedEventHandler?.OnDeserializeOrMappingError(e, eventPosition, eventType, timestamp, serializedPayload);
                if (errorPayload == null)
                    continue;
                eventPayload = errorPayload;
            }

            yield return eventFactory.EventFromPayload(eventPayload, eventPosition, timestamp);
        }
    }
}