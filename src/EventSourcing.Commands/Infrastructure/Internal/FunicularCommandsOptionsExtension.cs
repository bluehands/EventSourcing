﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using EventSourcing.Funicular.Commands.JsonPayload;
using EventSourcing.Infrastructure;
using EventSourcing.Infrastructure.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EventSourcing.Funicular.Commands.Infrastructure.Internal;

public record FunicularCommandsOptionsExtension(IReadOnlyCollection<Assembly>? CommandProcessorAssemblies) : IEventSourcingOptionsExtension
{
    public FunicularCommandsOptionsExtension() : this(default(IReadOnlyCollection<Assembly>))
    {
    }

    public void SetDefaults(EventSourcingOptionsBuilder builder)
    {
    }

    public void ApplyServices(IServiceCollection serviceCollection)
    {
        serviceCollection
            .AddSingleton<CommandBus>()
            .AddSingleton<ICommandBus>(sp => sp.GetRequiredService<CommandBus>())
            .AddSingleton<CommandProcessorSubscription>()
            .AddSingleton<EventReplayState>()
            .AddSingleton<EventSourcingContext, FunicularEventSourcingContext>()
            .AddInitializer<FunicularCommandsInitializer>();

        serviceCollection.AddTransient<CommandProcessor<NoopCommand>, NoopCommandProcessor>();
        AddCommandProcessors(serviceCollection, CommandProcessorAssemblies ?? new[] { EventSourcingOptionDefaults.DefaultImplementationAssembly });
    }

    public void AddDefaultServices(IServiceCollection serviceCollection, EventSourcingOptions eventSourcingOptions)
    {
        var lifetime = eventSourcingOptions.FindExtension<EventSourcingOptionsExtension>()?.EventPayloadMapperLifetime ?? EventSourcingOptionDefaults.DefaultEventPayloadMapperLifetime;
        serviceCollection.Add(new(typeof(EventPayloadMapper), typeof(CommandProcessedMapper), lifetime));
    }

    static IServiceCollection AddCommandProcessors(IServiceCollection serviceCollection, IEnumerable<Assembly> commandProcessorAssemblies)
    {
        var tuples = typeof(CommandProcessor)
            .GetConcreteDerivedTypes(commandProcessorAssemblies)
            .Select(processorType =>
            {
                var commandProcessorType = processorType.GetBaseType(t =>
                    t.IsGenericType && t.GetGenericTypeDefinition() == typeof(CommandProcessor<>));
                return (ServiceType: commandProcessorType, implementationType: processorType);
            })
            .GroupBy(t => t.ServiceType, t => t.implementationType)
            .Select(g =>
            {
                var implementations = g.ToImmutableArray();
                if (implementations.Length > 1)
                    throw new($"Found multiple processors for command of type {g.Key.GetGenericArguments()[0]}: {string.Join(",", implementations)}");

                return (
                    ServiceType: g.Key,
                    implementationType: implementations[0]
                );

            });

        foreach (var (serviceType, implementationType) in tuples)
            serviceCollection.Add(ServiceDescriptor.Describe(serviceType, implementationType, ServiceLifetime.Transient));

        return serviceCollection;
    }
}

sealed class CommandProcessorSubscription(
    CommandBus commandBus,
    IServiceScopeFactory serviceScopeFactory,
    WakeUp? wakeUp = null,
    ILogger<CommandBus>? logger = null)
    : IDisposable
{
    IDisposable? _subscription;

    internal void SubscribeCommandProcessors()
    {
        _subscription = commandBus.SubscribeCommandProcessors(commandType =>
        {
            var commandProcessorType = typeof(CommandProcessor<>).MakeGenericType(commandType);

            var scope = serviceScopeFactory.CreateScope();
            var processor = (CommandProcessor)scope.ServiceProvider.GetService(commandProcessorType);
            if (processor == null)
            {
                scope.Dispose();
                return null;
            }

            return new(processor, scope);
        }, () =>
        {
            var scope = serviceScopeFactory.CreateScope();
            return new(scope.ServiceProvider.GetRequiredService<IEventStore>(), scope);

        }, logger, wakeUp);
    }

    public void Dispose() => _subscription?.Dispose();
}