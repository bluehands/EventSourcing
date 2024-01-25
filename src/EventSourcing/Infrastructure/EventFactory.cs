using System;
using System.Collections.Immutable;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.Extensions.Logging;

namespace EventSourcing.Infrastructure;

public sealed class EventFactory
{
    readonly ILogger<EventFactory>? _logger;

    delegate Event CreateEvent(long version, DateTimeOffset timestamp, EventPayload payload);

    ImmutableDictionary<Type, CreateEvent> _eventFactoryByPayloadType = ImmutableDictionary<Type, CreateEvent>.Empty;

    public EventFactory(ILogger<EventFactory>? logger = null) => _logger = logger;

    static CreateEvent BuildCreateEvent(Type payloadType)
    {
        var eventType = typeof(Event<>).MakeGenericType(payloadType);
        var versionParam = Expression.Parameter(typeof(long));
        var timestampParam = Expression.Parameter(typeof(DateTimeOffset));
        var payloadParam = Expression.Parameter(typeof(EventPayload));

        var expression = Expression.Lambda<CreateEvent>(
            Expression.New(eventType.GetConstructors().Single(),
                versionParam,
                timestampParam,
                Expression.Convert(payloadParam, payloadType)),
            versionParam,
            timestampParam,
            payloadParam);
        var createEvent = expression.Compile();
        return createEvent;
    }

    public Event EventFromPayload(EventPayload eventPayload, long version, DateTimeOffset timestamp)
    {
        var payloadType = eventPayload.GetType();
        if (!_eventFactoryByPayloadType.TryGetValue(payloadType, out var createEvent))
        {
            _logger?.LogDebug("Creating factory for event payload {PayloadType}", payloadType);
            createEvent = BuildCreateEvent(payloadType);
            _eventFactoryByPayloadType = _eventFactoryByPayloadType.SetItem(payloadType, createEvent);
        }
        return createEvent(version, timestamp, eventPayload);
    }
}