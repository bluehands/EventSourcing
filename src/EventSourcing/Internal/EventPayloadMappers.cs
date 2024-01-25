using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;

namespace EventSourcing.Internal;

public class EventPayloadMappers
{
    readonly ImmutableDictionary<string, EventPayloadMapper> _mappersByEventType;

    public EventPayloadMappers(IEnumerable<EventPayloadMapper> mappers)
    {
        _mappersByEventType = mappers.Select(mapper =>
            {
                var serializableEventPayloadType = mapper
                    .GetType()
                    .GetArgumentOfFirstGenericBaseType(1);
                var tuple = new
                {
                    mapper,
                    attribute = serializableEventPayloadType
                        .GetCustomAttribute<SerializableEventPayloadAttribute>()
                };

                if (tuple.attribute == null)
                    throw new ArgumentException(
                        $"Type {serializableEventPayloadType.BeautifulName()} is used as payload type in PayloadMapper {tuple.mapper.GetType().BeautifulName()}. It has to be marked with {nameof(SerializableEventPayloadAttribute)}.");

                return tuple;
            })
            .ToImmutableDictionary(t => t.attribute.EventType, t => t.mapper);
    }

    public EventPayload MapFromSerializedPayload(StreamId streamId, string eventType, object serializedPayload,
        Func<Type, object, object>? deserializePayload = null)
    {
        if (!_mappersByEventType.TryGetValue(eventType, out var mapper))
        {
            throw new($"No payload mapper registered for event type {eventType}");
        }

        return mapper.InternalMapFromSerializablePayload(serializedPayload, streamId, deserializePayload);
    }

    public object MapToSerializablePayload(EventPayload payload)
    {
        var eventType = payload.EventType;
        if (!_mappersByEventType.TryGetValue(eventType, out var mapper))
        {
            throw new($"No payload mapper registered for event type {eventType}");
        }

        return mapper.InternalMapToSerializablePayload(payload);
    }
}