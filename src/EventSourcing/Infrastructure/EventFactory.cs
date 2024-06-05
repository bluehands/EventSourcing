using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Linq.Expressions;
using EventSourcing.Infrastructure.Internal;
using Microsoft.Extensions.Logging;

namespace EventSourcing.Infrastructure;

public sealed class EventFactory
{
    readonly ILogger<EventFactory>? _logger;

    delegate Event CreateEvent(long version, DateTimeOffset timestamp, IEventPayload payload);

    FrozenDictionary<Type, CreateEvent> _eventFactoryByPayloadType;

    public EventFactory(IEnumerable<EventPayloadMapper> payloadMappers, ILogger<EventFactory>? logger = null)
    {
        _logger = logger;
        _eventFactoryByPayloadType = WarmupFactoryCache(payloadMappers);
    }

    static FrozenDictionary<Type, CreateEvent> WarmupFactoryCache(IEnumerable<EventPayloadMapper> payloadMappers)
    {
        var payloadTypes = payloadMappers.Select(p => p.GetType()
            .GetArgumentOfFirstGenericBaseType(t => t.GetGenericTypeDefinition() == typeof(EventPayloadMapper<,>)));
        return payloadTypes
            .ToFrozenDictionary(p => p, BuildCreateEvent);
    }

    static CreateEvent BuildCreateEvent(Type payloadType)
    {
        var eventType = typeof(Event<>).MakeGenericType(payloadType);
        var versionParam = Expression.Parameter(typeof(long));
        var timestampParam = Expression.Parameter(typeof(DateTimeOffset));
        var payloadParam = Expression.Parameter(typeof(IEventPayload));

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

    public Event EventFromPayload(IEventPayload eventPayload, long version, DateTimeOffset timestamp)
    {
        var payloadType = eventPayload.GetType();
        if (!_eventFactoryByPayloadType.TryGetValue(payloadType, out var createEvent))
        {
            _logger?.LogDebug("Creating factory for event payload {PayloadType}", payloadType);
            createEvent = BuildCreateEvent(payloadType);
            _eventFactoryByPayloadType = _eventFactoryByPayloadType
                .ToImmutableDictionary()
                .SetItem(payloadType, createEvent)
                .ToFrozenDictionary();
        }
        return createEvent(version, timestamp, eventPayload);
    }
}