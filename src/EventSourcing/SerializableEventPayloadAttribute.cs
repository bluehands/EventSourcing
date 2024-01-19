using System;

namespace EventSourcing;

[AttributeUsage(AttributeTargets.Class)]
public sealed class SerializableEventPayloadAttribute(string eventType) : Attribute
{
    public string EventType { get; } = eventType;
}