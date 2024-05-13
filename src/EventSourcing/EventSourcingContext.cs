using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EventSourcing.Infrastructure;
using EventSourcing.Infrastructure.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EventSourcing;

public class EventSourcingContext(
    IServiceScopeFactory scopeFactory,
    ILogger<EventSourcingContext>? logger = null)
{
    public virtual async Task Initialize()
    {
        using var scope = scopeFactory.CreateScope();
        var serviceProvider = scope.ServiceProvider;

        var orderedInitializers = serviceProvider.GetServices<IInitializer>().SelectMany(initializer =>
        {
            var initializerType = initializer.GetType();
            var phaseAttributes = initializerType.AssertInitializationPhaseAttributes();
            return phaseAttributes.Select(attribute => (attribute.phaseAttribute, attribute.phaseType, initializer));
        })
        .GroupBy(t => (t.phaseType, t.phaseAttribute.Order))
        .OrderBy(t => t.Key.Order);

        foreach (var initializersOfPhase in orderedInitializers)
        {
            await Initialize(initializersOfPhase.Key.phaseType, initializersOfPhase.Select(i => i.initializer));
        }
    }

    protected virtual async Task Initialize(Type phase, IEnumerable<IInitializer> initializers) =>
        await Task.WhenAll(initializers.Select(async initializer =>
        {
            var initializerName = initializer.GetType().Name;
            logger?.LogInformation("Running initializer {Initializer} in phase {InitializationPhase}.", initializerName, phase.Name);
            await initializer.Initialize();
            logger?.LogInformation("{Initializer} initialize done.", initializerName);
        }));
}