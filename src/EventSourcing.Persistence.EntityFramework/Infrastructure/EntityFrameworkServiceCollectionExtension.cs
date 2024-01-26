using EventSourcing.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace EventSourcing.Persistence.EntityFramework.Infrastructure;

public static class EntityFrameworkServiceCollectionExtension
{
    public static IServiceCollection AddEntityFrameworkServices(this IServiceCollection services) =>
        services
            .AddInitializer<DatabaseInitializer>()
            .AddDefaultServices<Event, string, EventStore, EventStore, JsonEventSerializer, EventDescriptor>();
}