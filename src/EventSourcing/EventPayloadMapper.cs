using System;

namespace EventSourcing;

public abstract class EventPayloadMapper<TEventPayload, TSerializablePayload> : EventPayloadMapper
    where TEventPayload : IEventPayload
{
    internal override IEventPayload InternalMapFromSerializablePayload(object eventStoreEvent, StreamId streamId,
        Func<Type, object, object>? deserializePayload = null)
    {
        var serializablePayload = deserializePayload?.Invoke(typeof(TSerializablePayload), eventStoreEvent) ?? eventStoreEvent;
        var fromEvent = MapFromSerializablePayload((TSerializablePayload)serializablePayload, streamId);
        return fromEvent;
    }

    internal override object InternalMapToSerializablePayload(IEventPayload payload)
    {
        var outgoingEvent = (TEventPayload)payload;
        var serializableEvent = MapToSerializablePayload(outgoingEvent)!;
        return serializableEvent;
    }

    protected abstract TEventPayload MapFromSerializablePayload(TSerializablePayload serialized, StreamId streamId);

    protected abstract TSerializablePayload MapToSerializablePayload(TEventPayload payload);
}

public abstract class EventPayloadMapper
{
    internal abstract IEventPayload InternalMapFromSerializablePayload(object eventStoreEvent,
        StreamId streamId,
        Func<Type, object, object>? deserializePayload = null);

    internal abstract object InternalMapToSerializablePayload(IEventPayload payload);
}

sealed class IdentityMapper<T> : EventPayloadMapper<T, T> where T : IEventPayload
{
    protected override T MapFromSerializablePayload(T serialized, StreamId streamId) => serialized;

    protected override T MapToSerializablePayload(T payload) => payload;
}