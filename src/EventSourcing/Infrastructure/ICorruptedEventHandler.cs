using Microsoft.Extensions.Logging;
using System;

namespace EventSourcing.Infrastructure;

/// <summary>
/// Add an ICorruptedEventHandler to decide what to do with unreadable events.
///     <list type="bullet">
///         <item><description>Return null to skip the event, default if no ICorruptedEventHandler is added</description></item>
///         <item><description>Return an error payload to handle it on higher level</description></item>
///         <item><description>Throw an exception which actually bubbles up in the reader (EventStream might never get over that depending on the implementation)</description></item>
///     </list>
/// </summary>
public interface ICorruptedEventHandler
{
    IEventPayload? OnDeserializeOrMappingError(Exception error, long eventPosition, string eventType, DateTimeOffset timestamp, object serializedPayload);
}

public class LogAndIgnoreCorruptedEventHandler(ILogger<LogAndIgnoreCorruptedEventHandler>? logger = null)
    : ICorruptedEventHandler
{
    public IEventPayload? OnDeserializeOrMappingError(Exception error, long eventPosition, string eventType,
        DateTimeOffset timestamp, object serializedPayload)
    {
        logger?.LogError(error, $"Deserialize / map event at position {eventPosition} failed. EventType: {eventType}, Timestamp: {timestamp}, Serialized payload: {serializedPayload}");
        return null;
    }
}