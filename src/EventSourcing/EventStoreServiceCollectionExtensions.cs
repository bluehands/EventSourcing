using System;
using System.Threading.Tasks;
using EventSourcing;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class EventStoreServiceCollectionExtensions
{
    public static IServiceCollection AddEventSourcing(this IServiceCollection serviceCollection, Action<EventSourcingOptionsBuilder> configure)
    {
        var builder = EventSourcingOptionsBuilder.WithCoreOptions();
        configure(builder);

		foreach (var eventSourcingOptionsExtension in builder.Options.Extensions)
        {
            eventSourcingOptionsExtension.ApplyServices(serviceCollection);
        }

        return serviceCollection;
    }

    public static async Task StartEventSourcing(this IServiceProvider serviceProvider)
    {
        var eventSourcingContext = serviceProvider.GetRequiredService<EventSourcingContext>();
        await eventSourcingContext.Initialize();
    }
}