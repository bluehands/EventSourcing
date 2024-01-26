using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using EventSourcing.Infrastructure;
using EventSourcing.Infrastructure.Internal;
using Microsoft.Extensions.Logging;

namespace EventSourcing;

public class EventSourcingContext(
    IEnumerable<IInitializer> initializers,
    ILogger<EventSourcingContext>? logger = null)
{
    readonly IReadOnlyCollection<IInitializer> _initializers = initializers.ToImmutableArray();

    public virtual async Task Initialize()
    {
        var orderedInitializers = _initializers.SelectMany(initializer =>
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

    protected virtual async Task Initialize(Type phase, IEnumerable<IInitializer> initializers)
    {
        await Task.WhenAll(initializers.Select(async initializer =>
        {
            var initializerName = initializer.GetType().Name;
            logger?.LogInformation("Running initializer {Initializer} in phase {InitializationPhase}.", initializerName, phase.Name);
            await initializer.Initialize();
            logger?.LogInformation("{Initializer} done.", initializerName);
        }));
    }
}