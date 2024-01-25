using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using EventSourcing.Infrastructure;
using EventSourcing.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EventSourcing;

public class EventSourcingOptions : IEventSourcingOptions
{
    readonly ImmutableDictionary<Type, IEventSourcingOptionsExtension> _extensions = ImmutableDictionary<Type, IEventSourcingOptionsExtension>.Empty;
    public IEnumerable<IEventSourcingOptionsExtension> Extensions => _extensions.Values;

    public EventSourcingOptions()
    {
    }

    public EventSourcingOptions(ImmutableDictionary<Type, IEventSourcingOptionsExtension> extensions) => _extensions = extensions;

    public TExtension? FindExtension<TExtension>() => _extensions.TryGetValue(typeof(TExtension), out var extension) ? (TExtension?)extension : default;

    public TExtension GetExtension<TExtension>()
    {
        var extension = FindExtension<TExtension>();
        return extension == null
            ? throw new InvalidOperationException($"Option extension of type {typeof(TExtension).Name} not found")
            : extension;
    }

    public EventSourcingOptions WithExtension<TExtension>(TExtension extension)
        where TExtension : IEventSourcingOptionsExtension =>
        new(_extensions.SetItem(typeof(TExtension), extension));
}

public class EventSourcingOptionsBuilder : IEventSourcingOptionsBuilderInfrastructure
{
    EventSourcingOptions _options;
    public EventSourcingOptions Options => _options;

    public static EventSourcingOptionsBuilder WithCoreOptions() => new(new EventSourcingOptions().WithExtension(new EventSourcingOptionsExtension()));

    public EventSourcingOptionsBuilder() : this(new()) { }

    public EventSourcingOptionsBuilder(EventSourcingOptions options) => _options = options ?? throw new ArgumentNullException(nameof(options));

    public EventSourcingOptionsBuilder PayloadAssemblies(Assembly assembly, params Assembly[] assemblies) =>
        WithOption(e => e with
        {
            PayloadAssemblies = ImmutableList.Create<Assembly>().Add(assembly).AddRange(assemblies)
        });

    public EventSourcingOptionsBuilder PayloadMapperAssemblies(Assembly assembly, params Assembly[] assemblies) =>
        WithOption(e => e with
        {
            PayloadMapperAssemblies = ImmutableList.Create<Assembly>().Add(assembly).AddRange(assemblies)
        });

    public EventSourcingOptionsBuilder WithCorruptedEventsHandler<T>(ServiceLifetime lifetime = ServiceLifetime.Transient) where T : ICorruptedEventHandler =>
        WithOption(e => e with
        {
            CorruptedEventsHandlerDescriptor = new(typeof(ICorruptedEventHandler), typeof(T), lifetime)
        });

    EventSourcingOptionsBuilder WithOption(Func<EventSourcingOptionsExtension, EventSourcingOptionsExtension> withFunc)
    {
        var optionsExtension = withFunc(_options.FindExtension<EventSourcingOptionsExtension>() ?? new EventSourcingOptionsExtension());
        AddOrUpdateExtension(optionsExtension);
        return this;
    }

    void AddOrUpdateExtension(EventSourcingOptionsExtension optionsExtension) => ((IEventSourcingOptionsBuilderInfrastructure)this).AddOrUpdateExtension(optionsExtension);

    void IEventSourcingOptionsBuilderInfrastructure.AddOrUpdateExtension<TExtension>(TExtension extension) => _options = _options.WithExtension(extension);
}

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

        var payloadAssemblies = PayloadAssemblies ?? new[] { Defaults.DefaultImplementationAssembly };
        RegisterPayloadMappers(serviceCollection, PayloadMapperAssemblies ?? new[] { Defaults.DefaultImplementationAssembly }, payloadAssemblies, EventPayloadMapperLifetime ?? Defaults.DefaultEventPayloadMapperLifetime);

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

public static class Defaults
{
    public static Assembly DefaultImplementationAssembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
    public static ServiceLifetime DefaultEventPayloadMapperLifetime = ServiceLifetime.Singleton;
}