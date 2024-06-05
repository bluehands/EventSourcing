using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace EventSourcing.Infrastructure.Internal;

public class EventPayloadMappers
{
    readonly FrozenDictionary<string, EventPayloadMapper> _mappersByEventType;

    public EventPayloadMappers(IEnumerable<EventPayloadMapper> mappers)
    {
        _mappersByEventType = mappers.Select(mapper =>
            {
                var serializableEventPayloadType = mapper
                    .GetType()
                    .GetArgumentOfFirstGenericBaseType(
                        t => t.GetGenericTypeDefinition() == typeof(EventPayloadMapper<,>), argumentIndex: 1);
                var tuple = new
                {
                    mapper,
                    attribute = serializableEventPayloadType.GetCustomAttribute<SerializableEventPayloadAttribute>()
                };

                if (tuple.attribute == null)
                    throw new ArgumentException(
                        $"Type {serializableEventPayloadType.BeautifulName()} is used as payload type in PayloadMapper {tuple.mapper.GetType().BeautifulName()}. It has to be marked with {nameof(SerializableEventPayloadAttribute)}.");

                return tuple;
            })
            .ToFrozenDictionary(t => t.attribute.EventType, t => t.mapper);
    }

    public IEventPayload MapFromSerializedPayload(StreamId streamId, string eventType, object serializedPayload,
        Func<Type, object, object>? deserializePayload = null)
    {
        if (!_mappersByEventType.TryGetValue(eventType, out var mapper))
        {
            throw new(NoPayloadMapperMessage(eventType));
        }

        return mapper.InternalMapFromSerializablePayload(serializedPayload, streamId, deserializePayload);
    }

    public object MapToSerializablePayload(IEventPayload payload)
    {
        var eventType = payload.EventType;
        if (!_mappersByEventType.TryGetValue(eventType, out var mapper))
        {
            throw new(NoPayloadMapperMessage(eventType));
        }

        return mapper.InternalMapToSerializablePayload(payload);
    }

    static string NoPayloadMapperMessage(string eventType) =>
        $"""
         No payload mapper registered for event type {eventType}.
         Please define serializable payload type marked with [SerializablePayloadType("{eventType}")] attribute and implement mapper derived from EventPayloadMapper<,>.
         SerializablePayloadType attribute can be used on payload types directly but that is recommended for testing and prototyping purposes only.
         """;
}