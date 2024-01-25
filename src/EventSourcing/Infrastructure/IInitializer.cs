using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace EventSourcing.Infrastructure;

public interface IInitializer
{
    Task Initialize();
}

// ReSharper disable once UnusedTypeParameter
public interface IInitializer<TPhase> : IInitializer where TPhase : IInitializationPhase;

public interface IInitializationPhase;

public sealed class SchemaInitialization : IInitializationPhase;
public sealed class BeforeEventsReplay : IInitializationPhase;

public static class InitializerServiceCollectionExtension
{
    public static IServiceCollection AddInitializer<T>(this IServiceCollection serviceCollection)
        where T : class, IInitializer
    {
        if (typeof(T).GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IInitializer<>)))
            return serviceCollection.AddTransient<IInitializer, T>();
        throw new InvalidOperationException($"{typeof(T)} derives from IInitializer directly. Please derive from IInitializer<TPhase> to specify phase when initializer is called.");
    }
}