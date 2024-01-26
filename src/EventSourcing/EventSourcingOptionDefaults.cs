using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace EventSourcing;

public static class EventSourcingOptionDefaults
{
    public static Assembly DefaultImplementationAssembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
    public static ServiceLifetime DefaultEventPayloadMapperLifetime = ServiceLifetime.Singleton;
}