using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EventSourcing.Infrastructure;

public static class EventSourcingServiceCollectionExtensions
{
    public static IServiceCollection AddDefaultServices<TDbEvent, TSerializedPayload, TEventReader, TEventWriter, TEventSerializer, TEventDescriptor>(
        this IServiceCollection services)
        where TEventReader : class, IEventReader<TDbEvent>
        where TEventWriter : class, IEventWriter<TDbEvent>
        where TEventSerializer : class, IEventSerializer<TSerializedPayload>
        where TEventDescriptor : class, IDbEventDescriptor<TDbEvent, TSerializedPayload>
    {
        services.TryAddTransient<IEventReader<TDbEvent>, TEventReader>();
        services.TryAddTransient<IEventWriter<TDbEvent>, TEventWriter>();
        services.TryAddTransient<IEventSerializer<TSerializedPayload>, TEventSerializer>();
        services.TryAddTransient<IDbEventDescriptor<TDbEvent, TSerializedPayload>, TEventDescriptor>();
        services.TryAddTransient<IEventStore, EventStore<TDbEvent, TSerializedPayload>>();
        services.TryAddTransient<IEventMapper<TDbEvent>, EventStore<TDbEvent, TSerializedPayload>>();
        return services;
    }



    public static bool EventStreamOptionsConfigured(this EventSourcingOptionsBuilder optionsBuilder) => optionsBuilder.Options.Extensions.OfType<IEventStreamOptionsExtension>().Any();
}