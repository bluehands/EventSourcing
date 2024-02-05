using System;

namespace EventSourcing;

public abstract record Event(long Position, DateTimeOffset Timestamp, IEventPayload Payload)
{
    public StreamId StreamId => Payload.StreamId;

    public string Type => Payload.EventType;

    public override string ToString() =>
        $"{nameof(StreamId)}: {StreamId}, {nameof(Type)}: {Type}, {nameof(Position)}: {Position}, {nameof(Timestamp)}: {Timestamp}, {nameof(Payload)}: {Payload}";
}

public sealed record Event<T>(long Position, DateTimeOffset Timestamp, T Payload)
    : Event(Position, Timestamp, Payload) where T : IEventPayload
{
    public new T Payload => (T)base.Payload;
}