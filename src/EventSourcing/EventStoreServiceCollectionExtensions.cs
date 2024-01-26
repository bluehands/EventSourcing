using System;
using System.Linq;
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

        var optionsExtensions = builder.Options.Extensions.ToList();
        foreach (var eventSourcingOptionsExtension in optionsExtensions)
        {
            eventSourcingOptionsExtension.SetDefaults(builder);
        }

        foreach (var eventSourcingOptionsExtension in builder.Options.Extensions)
        {
            eventSourcingOptionsExtension.ApplyServices(serviceCollection);
        }

        foreach (var eventSourcingOptionsExtension in builder.Options.Extensions)
        {
            eventSourcingOptionsExtension.AddDefaultServices(serviceCollection, builder.Options);
        }

        return serviceCollection;
    }

    public static async Task StartEventSourcing(this IServiceProvider serviceProvider)
    {
        var eventSourcingContext = serviceProvider.GetRequiredService<EventSourcingContext>();
        await eventSourcingContext.Initialize();
    }
}