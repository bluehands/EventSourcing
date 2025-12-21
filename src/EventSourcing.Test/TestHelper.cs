using EventSourcing.Persistence.EntityFramework.Sqlite.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EventSourcing.Test;

static class TestHelper
{
    public static async Task<TTestService> SetupEventSourcing<TTestService>(
        Action<EventSourcingOptionsBuilder>? optionAction = null,
        Action<IServiceCollection>? modifyServices = null,
        Action<InMemoryEventStoreOptionsBuilder>? inMemoryOptions = null)
        where TTestService : class
    {
        var serviceCollection = new ServiceCollection()
            .AddTransient<TTestService>()
            .AddEventSourcing(options =>
            {
                options
                    .UseInMemoryEventStore(inMemoryOptions)
                    .PayloadAssemblies(typeof(HandleBadCasesTest).Assembly);

                optionAction?.Invoke(options);
            })
            .AddLogging(l => l.AddConsole());
        modifyServices?.Invoke(serviceCollection);

        var serviceProvider = serviceCollection.BuildServiceProvider();

        await serviceProvider
            .StartEventSourcing();

        return serviceProvider.GetRequiredService<TTestService>();
    }
}