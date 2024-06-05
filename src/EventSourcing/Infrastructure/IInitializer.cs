using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EventSourcing.Infrastructure.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace EventSourcing.Infrastructure;

/// <summary>
/// Marker interface for initializers. Implementors won't be constructed.
/// </summary>
/// <typeparam name="TPhase"></typeparam>
public interface IInitializer<TPhase> : IInitializer where TPhase : IInitializationPhase;

public interface IBeforeEventReplayInitializer : IInitializer<BeforeEventReplay>;

[AttributeUsage(AttributeTargets.Class)]
public class InitializationPhaseAttribute(int order) : Attribute
{
    public int Order { get; } = order;
}

/// <summary>
/// Marker interface for initialization phases. Implementors has to be decorated InitializationPhaseAttribute and won't be constructed.
/// </summary>
public interface IInitializationPhase;

public static class InitializationPhaseOrders
{
    public const int SchemaInitialization = 0;
    public const int BeforeEventReplay = 100;
    public const int EventReplayStarting = 200;
    public const int EventReplayStarted = 300;
}

[InitializationPhase(InitializationPhaseOrders.SchemaInitialization)]
public sealed class SchemaInitialization : IInitializationPhase;

[InitializationPhase(InitializationPhaseOrders.BeforeEventReplay)]
public sealed class BeforeEventReplay : IInitializationPhase;

[InitializationPhase(InitializationPhaseOrders.EventReplayStarting)]
public sealed class EventReplayStarting : IInitializationPhase;

[InitializationPhase(InitializationPhaseOrders.EventReplayStarted)]
public sealed class EventReplayStarted : IInitializationPhase;

public static class InitializerServiceCollectionExtensions
{
    public static IServiceCollection AddInitializer<T>(this IServiceCollection serviceCollection)
        where T : class, IInitializer
    {
        AssertInitializationPhaseAttributes(typeof(T));
        return serviceCollection.AddTransient<IInitializer, T>();
    }

    internal static IReadOnlyCollection<(Type phaseType, InitializationPhaseAttribute phaseAttribute)> AssertInitializationPhaseAttributes(this Type initializerType)
    {
        var phaseAttributes = initializerType
            .GetInterfaces()
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IInitializer<>))
            .Select(i =>
            {
                var phaseType = i.GetGenericArguments()[0];
                var phaseAttribute = phaseType.GetCustomAttribute<InitializationPhaseAttribute>();
                if (phaseAttribute == null)
                    throw new InvalidOperationException(
                        $"{phaseType} has to be decorated with InitializationPhaseAttribute because it is used as argument in initializer type {initializerType}");
                return (phaseType, phaseAttribute);
            })
            .ToList();

        if (phaseAttributes.Count == 0)
        {
            throw new InvalidOperationException($"{initializerType} implements IInitializer directly. Please implement IInitializer<TPhase> to specify phase when initializer is called.");
        }

        return phaseAttributes;
    }
}