using System;
using System.Threading.Tasks;
using EventSourcing.Infrastructure;
using EventSourcing.Infrastructure.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EventSourcing;

public interface IAllowPollingEventStreamBuilder;

public static class PollingEventStreamExtensions
{
    public static TBuilder UsePollingEventStream<TBuilder>(this TBuilder builder, TimeSpan minWaitTime, TimeSpan maxWaitTime, Func<Task<long>>? getPositionToStartFrom = null) 
        where TBuilder : IAllowPollingEventStreamBuilder, IEventSourcingExtensionsBuilderInfrastructure =>
        builder.WithOption<TBuilder, PollingEventStreamOptionsExtension>(e => e with
        {
            MinWaitTime = minWaitTime,
            MaxWaitTime = maxWaitTime,
            GetPositionToStartFrom = getPositionToStartFrom
        });

    public static TBuilder UsePollingEventStream<TBuilder>(this TBuilder builder) 
        where TBuilder : IAllowPollingEventStreamBuilder, IEventSourcingExtensionsBuilderInfrastructure =>
        builder.WithOption<TBuilder, PollingEventStreamOptionsExtension>(e => e);
}

public record PollingEventStreamOptionsExtension(TimeSpan? MinWaitTime, TimeSpan? MaxWaitTime, Func<Task<long>>? GetPositionToStartFrom) : IEventStreamOptionsExtension
{
    public PollingEventStreamOptionsExtension() : this(null, null, null)
    {
    }

    public void SetDefaults(EventSourcingOptionsBuilder builder)
    {
    }

    public void ApplyServices(IServiceCollection serviceCollection)
    {
        serviceCollection.AddSingleton(sp => new WakeUp(MinWaitTime ?? TimeSpan.Zero, MaxWaitTime ?? TimeSpan.FromMilliseconds(100), sp.GetService<ILogger<WakeUp>>()));
        serviceCollection.AddSingleton(sp => BuildPollingEventStream(sp, GetPositionToStartFrom ?? (() => Task.FromResult(0L))));
        serviceCollection.AddSingleton<IObservable<Event>>(sp => sp.GetRequiredService<EventStream<Event>>());
    }

    public void AddDefaultServices(IServiceCollection serviceCollection, EventSourcingOptions eventSourcingOptions)
    {
    }

    static EventStream<Event> BuildPollingEventStream(IServiceProvider provider, Func<Task<long>> getPositionToStartFrom)
    {
        var streamScope = provider.CreateScope();

        var eventReader = streamScope.ServiceProvider.GetRequiredService<IEventStore>();
        var wakeUp = provider.GetRequiredService<WakeUp>();

        var events = PollingObservable.Poll(
            getPositionToStartFrom,
            l =>  eventReader.ReadEvents(l),
            wakeUp,
            provider.GetService<ILogger<EventStream<Event>>>()
        );

        return new(events, streamScope);
    }
}