using Microsoft.Extensions.DependencyInjection;
using System.Linq;

namespace EventSourcing.Infrastructure;

public static class EventSourcingServiceCollectionExtensions
{
    public static IServiceCollection AddDefaultServices<TDbEvent, TSerializedPayload, TEventReader, TEventWriter, TEventSerializer, TEventDescriptor>(
        this IServiceCollection services) 
        where TEventReader : class, IEventReader<TDbEvent> 
        where TEventWriter : class, IEventWriter<TDbEvent>
        where TEventSerializer : class, IEventSerializer<TSerializedPayload>
        where TEventDescriptor : class, IDbEventDescriptor<TDbEvent, TSerializedPayload> =>
        services
            .AddTransient<IEventReader<TDbEvent>, TEventReader>()
            .AddTransient<IEventWriter<TDbEvent>, TEventWriter>()
            .AddTransient<IEventSerializer<TSerializedPayload>, TEventSerializer>()
            .AddTransient<IDbEventDescriptor<TDbEvent, TSerializedPayload>, TEventDescriptor>()
            .AddTransient<IEventStore, EventStore<TDbEvent, TSerializedPayload>>()
            .AddTransient<IEventMapper<TDbEvent>, EventStore<TDbEvent, TSerializedPayload>>();

    public static bool EventStreamOptionsConfigured(this EventSourcingOptionsBuilder optionsBuilder) => optionsBuilder.Options.Extensions.OfType<IEventStreamOptionsExtension>().Any();
}