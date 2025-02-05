using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using EventSourcing.Commands.SerializablePayloads;
using EventSourcing.Infrastructure;
using EventSourcing.Infrastructure.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EventSourcing.Commands.Infrastructure.Internal;

public record FunicularCommandsOptionsExtension<TError, TFailurePayload>(IReadOnlyCollection<Assembly>? CommandProcessorAssemblies) : IEventSourcingOptionsExtension
    where TFailurePayload : class, IErrorPayload<TError, TFailurePayload> where TError : notnull
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
            .AddSingleton<CommandProcessorSubscription<TError>>()
            .AddSingleton<EventReplayState<TError>>()
            .AddSingleton<IEventReplayState>(sp => sp.GetRequiredService<EventReplayState<TError>>())
            .AddSingleton<EventSourcingContext, FunicularEventSourcingContext>()
            .AddInitializer<CommandsInitializer<TError>>();

        serviceCollection.AddTransient<CommandProcessor<NoopCommand, TError>, NoopCommandProcessor<TError>>();
        AddCommandProcessors(serviceCollection, CommandProcessorAssemblies ?? [EventSourcingOptionDefaults.DefaultImplementationAssembly
        ]);
    }

    public void AddDefaultServices(IServiceCollection serviceCollection, EventSourcingOptions eventSourcingOptions)
    {
        var lifetime = eventSourcingOptions.FindExtension<EventSourcingOptionsExtension>()?.EventPayloadMapperLifetime ?? EventSourcingOptionDefaults.DefaultEventPayloadMapperLifetime;
        serviceCollection.Add(new(typeof(EventPayloadMapper), typeof(CommandProcessedMapper<TError, TFailurePayload>), lifetime));
    }

    static IServiceCollection AddCommandProcessors(IServiceCollection serviceCollection, IEnumerable<Assembly> commandProcessorAssemblies)
    {
        var tuples = typeof(CommandProcessor<TError>)
            .GetConcreteDerivedTypes(commandProcessorAssemblies)
            .Select(processorType =>
            {
                var commandProcessorType = processorType.GetBaseType(t =>
                    t.IsGenericType && t.GetGenericTypeDefinition() == typeof(CommandProcessor<,>));
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

sealed class CommandProcessorSubscription<TError>(
    CommandBus commandBus,
    IServiceScopeFactory serviceScopeFactory,
    WakeUp? wakeUp = null,
    ILogger<CommandBus>? logger = null)
    : IDisposable where TError : notnull
{
    IDisposable? _subscription;

    internal void SubscribeCommandProcessors()
    {
        _subscription = commandBus.SubscribeCommandProcessors<TError>(commandType =>
        {
            var commandProcessorType = typeof(CommandProcessor<,>).MakeGenericType(commandType, typeof(TError));

            var scope = serviceScopeFactory.CreateScope();
            var processor = (CommandProcessor<TError>?)scope.ServiceProvider.GetService(commandProcessorType);
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