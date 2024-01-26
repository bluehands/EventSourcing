using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EventSourcing.Infrastructure;
using EventSourcing.Infrastructure.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EventSourcing;

public record EventSourcingOptionsExtension(
    IReadOnlyCollection<Assembly>? PayloadAssemblies,
    IReadOnlyCollection<Assembly>? PayloadMapperAssemblies,
    ServiceDescriptor? CorruptedEventsHandlerDescriptor,
    ServiceLifetime? EventPayloadMapperLifetime)
    : IEventSourcingOptionsExtension
{
    public EventSourcingOptionsExtension() : this(null, null, null, null)
    {
    }

    public void SetDefaults(EventSourcingOptionsBuilder builder)
    {
    }

    public void ApplyServices(IServiceCollection serviceCollection)
    {
        serviceCollection.TryAddSingleton<EventSourcingContext>();
        serviceCollection.AddSingleton<EventFactory>();
        serviceCollection.AddInitializer<EventStreamStarter>();

        var payloadAssemblies = PayloadAssemblies ?? new[] { EventSourcingOptionDefaults.DefaultImplementationAssembly };
        RegisterPayloadMappers(serviceCollection, PayloadMapperAssemblies ?? new[] { EventSourcingOptionDefaults.DefaultImplementationAssembly }, payloadAssemblies, EventPayloadMapperLifetime ?? EventSourcingOptionDefaults.DefaultEventPayloadMapperLifetime);

        if (CorruptedEventsHandlerDescriptor != null)
        {
            serviceCollection.Add(CorruptedEventsHandlerDescriptor);
        }
        else
        {
            serviceCollection.TryAddTransient<ICorruptedEventHandler, LogAndIgnoreCorruptedEventHandler>();
        }
    }

    public void AddDefaultServices(IServiceCollection serviceCollection, EventSourcingOptions eventSourcingOptions)
    {
    }

    static void RegisterPayloadMappers(IServiceCollection serviceCollection, IEnumerable<Assembly> payloadMapperAssemblies, IEnumerable<Assembly> payloadAssemblies, ServiceLifetime mapperLifetime)
    {
        var mapperDescriptors = typeof(EventPayloadMapper)
            .GetConcreteDerivedTypes(payloadMapperAssemblies)
            .Select(t => new ServiceDescriptor(typeof(EventPayloadMapper), t, mapperLifetime));

        var identityMapperDescriptors = typeof(EventPayload)
            .GetConcreteDerivedTypes(payloadAssemblies.Distinct())
            .Select(payloadType =>
            {
                var serializableEventPayloadAttribute =
                    payloadType.GetCustomAttribute<SerializableEventPayloadAttribute>();
                if (serializableEventPayloadAttribute != null)
                {
                    return new ServiceDescriptor(typeof(EventPayloadMapper), typeof(IdentityMapper<>).MakeGenericType(payloadType), mapperLifetime);
                }

                return null;
            }).Where(s => s != null);

        serviceCollection.Add(mapperDescriptors.Concat(identityMapperDescriptors)!);
        serviceCollection.Add(new(typeof(EventPayloadMappers), typeof(EventPayloadMappers), mapperLifetime));
    }
}

