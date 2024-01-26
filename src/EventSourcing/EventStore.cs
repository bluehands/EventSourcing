using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EventSourcing.Infrastructure;
using EventSourcing.Infrastructure.Internal;

namespace EventSourcing;

public interface IEventStore
{
    IAsyncEnumerable<Event> ReadEvents(long fromPositionInclusive);
    Task WriteEvents(IReadOnlyCollection<EventPayload> payloads);
}

public class EventStore<TDbEvent, TSerializedPayload> : IEventStore, IEventMapper<TDbEvent>
{
    readonly IEventReader<TDbEvent> _eventReader;
    readonly IEventSerializer<TSerializedPayload> _payloadSerializer;
    readonly EventPayloadMappers _payloadMappers;
    readonly EventFactory _eventFactory;
    readonly ICorruptedEventHandler? _corruptedEventHandler;
    readonly IEventWriter<TDbEvent> _eventWriter;
    readonly IDbEventDescriptor<TDbEvent, TSerializedPayload> _eventDescriptor;

    public EventStore(
        IEventReader<TDbEvent> eventReader,
        IEventWriter<TDbEvent> eventWriter,
        IDbEventDescriptor<TDbEvent, TSerializedPayload> eventDescriptor,
        IEventSerializer<TSerializedPayload> payloadSerializer,
        EventPayloadMappers payloadMappers,
        EventFactory eventFactory,
        ICorruptedEventHandler? corruptedEventHandler = null
        )
    {
        _eventReader = eventReader;
        _payloadSerializer = payloadSerializer;
        _payloadMappers = payloadMappers;
        _eventFactory = eventFactory;
        _corruptedEventHandler = corruptedEventHandler;
        _eventWriter = eventWriter;
        _eventDescriptor = eventDescriptor;
    }

    public IAsyncEnumerable<Event> ReadEvents(long fromPositionInclusive)
    {
        var dbEvents = _eventReader.ReadEvents(fromPositionInclusive);
        return MapFromDbEvents(dbEvents);
    }

    public Task WriteEvents(IReadOnlyCollection<EventPayload> payloads)
    {
        var events = MapToDbEvents(payloads);
        return _eventWriter.WriteEvents(events);
    }

    public IEnumerable<TDbEvent> MapToDbEvents(IEnumerable<EventPayload> payloads) =>
        payloads.Select(payload =>
        {
            var serializablePayload = _payloadMappers.MapToSerializablePayload(payload);
            var serializedPayload = _payloadSerializer.Serialize(serializablePayload);
            return _eventDescriptor.CreateDbEvent(payload.StreamId, payload.EventType, serializedPayload);
        });

    public async IAsyncEnumerable<Event> MapFromDbEvents(IAsyncEnumerable<TDbEvent> dbEvents)
    {
        await foreach (var dbEvent in dbEvents)
        {
            EventPayload eventPayload;

            var eventType = _eventDescriptor.GetEventType(dbEvent);
            var streamId = _eventDescriptor.GetStreamId(dbEvent);
            var serializedPayload = _eventDescriptor.GetPayload(dbEvent)!;
            var eventPosition = _eventDescriptor.GetPosition(dbEvent);
            var timestamp = _eventDescriptor.GetTimestamp(dbEvent);

            try
            {
                eventPayload = _payloadMappers.MapFromSerializedPayload(streamId, eventType, serializedPayload, _payloadSerializer != null
                    ? (t, s) => _payloadSerializer.Deserialize(t, (TSerializedPayload)s)
                    : null);
            }
            catch (Exception e)
            {
                // permanent deserialize / mapping error. 
                var errorPayload = _corruptedEventHandler?.OnDeserializeOrMappingError(e, eventPosition, eventType, timestamp, serializedPayload);
                if (errorPayload == null)
                    continue;
                eventPayload = errorPayload;
            }

            yield return _eventFactory.EventFromPayload(eventPayload, eventPosition, timestamp);
        }
    }
}