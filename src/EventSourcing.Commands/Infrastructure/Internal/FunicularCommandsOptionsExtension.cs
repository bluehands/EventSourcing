using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using EventSourcing.Funicular.Commands.JsonPayload;
using EventSourcing.Infrastructure;
using EventSourcing.Internal;
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
        serviceCollection.AddSingleton<CommandStream>()
            .AddSingleton<CommandProcessorSubscription>()
            .AddInitializer<FunicularCommandsInitializer>();

        AddCommandProcessors(serviceCollection, CommandProcessorAssemblies ?? new[] { Defaults.DefaultImplementationAssembly });
    }

    public void AddDefaultServices(IServiceCollection serviceCollection, EventSourcingOptions eventSourcingOptions)
    {
        var lifetime = eventSourcingOptions.FindExtension<EventSourcingOptionsExtension>()?.EventPayloadMapperLifetime ?? Defaults.DefaultEventPayloadMapperLifetime;
        serviceCollection.Add(new(typeof(EventPayloadMapper), typeof(CommandProcessedMapper), lifetime));
    }

    static IServiceCollection AddCommandProcessors(IServiceCollection serviceCollection, IEnumerable<Assembly> commandProcessorAssemblies)
    {
        serviceCollection.AddSingleton<CommandStream>();

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

sealed class FunicularCommandsInitializer(CommandProcessorSubscription commandProcessorSubscription)
    : IInitializer<BeforeEventsReplay>
{
    public Task Initialize()
    {
        commandProcessorSubscription.SubscribeCommandProcessors();
        return Task.CompletedTask;
    }
}

sealed class CommandProcessorSubscription(
    CommandStream commandStream,
    IEventStore eventStore,
    IServiceScopeFactory serviceScopeFactory,
    WakeUp? wakeUp = null,
    ILogger<CommandStream>? logger = null)
    : IDisposable
{
    IDisposable? _subscription;

    internal void SubscribeCommandProcessors()
    {
        _subscription = commandStream.SubscribeCommandProcessors(commandType =>
        {
            var commandProcessorType = typeof(CommandProcessor<>).MakeGenericType(commandType);
            try
            {
                var scope = serviceScopeFactory.CreateScope();
                var processor = (CommandProcessor)scope.ServiceProvider.GetService(commandProcessorType);
                return new(processor, scope);
            }
            catch (Exception e)
            {
                logger?.LogError(e, $"Failed to resolve command processor of type {commandProcessorType}");
            }

            return null;
        }, eventStore, logger, wakeUp);
    }

    public void Dispose() => _subscription?.Dispose();
}