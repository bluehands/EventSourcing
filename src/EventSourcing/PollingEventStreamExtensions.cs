using System;
using System.Threading.Tasks;
using EventSourcing.Infrastructure;
using EventSourcing.Infrastructure.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EventSourcing;

public interface IAllowPollingEventStreamBuilder;

public static class PollingEventStreamDefaults
{
    public static readonly TimeSpan MinWaitTime = TimeSpan.Zero;
    public static readonly TimeSpan MaxWaitTime = TimeSpan.FromMilliseconds(100);
    public static readonly TimeSpan DelayOnError = TimeSpan.FromSeconds(1);
}

public static class PollingEventStreamExtensions
{
    public static TBuilder UsePollingEventStream<TBuilder>(this TBuilder builder, TimeSpan minWaitTime,
        TimeSpan maxWaitTime, Func<Task<long>>? getPositionToStartFrom = null, TimeSpan? delayOnError = null)
        where TBuilder : IAllowPollingEventStreamBuilder, IEventSourcingExtensionsBuilderInfrastructure =>
        builder.WithOption<TBuilder, PollingEventStreamOptionsExtension>(_ => new(
            MinWaitTime: minWaitTime,
            MaxWaitTime: maxWaitTime,
            DelayOnError: delayOnError,
            GetPositionToStartFrom: getPositionToStartFrom)
        );

    public static TBuilder UsePollingEventStream<TBuilder>(this TBuilder builder, Func<Task<long>> getPositionToStartFrom)
        where TBuilder : IAllowPollingEventStreamBuilder, IEventSourcingExtensionsBuilderInfrastructure =>
        builder.WithOption<TBuilder, PollingEventStreamOptionsExtension>(_ => new(
            MinWaitTime: null,
            MaxWaitTime: null,
            DelayOnError: null,
            GetPositionToStartFrom: getPositionToStartFrom)
        );

    public static TBuilder UsePollingEventStream<TBuilder>(this TBuilder builder) 
        where TBuilder : IAllowPollingEventStreamBuilder, IEventSourcingExtensionsBuilderInfrastructure =>
        builder.WithOption<TBuilder, PollingEventStreamOptionsExtension>(e => e);
}

public record PollingEventStreamOptionsExtension(
    TimeSpan? MinWaitTime,
    TimeSpan? MaxWaitTime,
    TimeSpan? DelayOnError,
    Func<Task<long>>? GetPositionToStartFrom) : IEventStreamOptionsExtension
{
    public PollingEventStreamOptionsExtension()
        : this(
            MinWaitTime: null,
            MaxWaitTime: null,
            DelayOnError: null,
            GetPositionToStartFrom: null)
    {
    }

    public void SetDefaults(EventSourcingOptionsBuilder builder)
    {
    }

    public void ApplyServices(IServiceCollection serviceCollection)
    {
        serviceCollection.AddSingleton(sp => new WakeUp(MinWaitTime ?? PollingEventStreamDefaults.MinWaitTime, MaxWaitTime ?? PollingEventStreamDefaults.MaxWaitTime, sp.GetService<ILogger<WakeUp>>()));
        serviceCollection.AddSingleton(sp => BuildPollingEventStream(sp, GetPositionToStartFrom ?? (() => Task.FromResult(0L)), DelayOnError ?? PollingEventStreamDefaults.DelayOnError));
        serviceCollection.AddSingleton<IObservable<Event>>(sp => sp.GetRequiredService<EventStream<Event>>());
    }

    public void AddDefaultServices(IServiceCollection serviceCollection, EventSourcingOptions eventSourcingOptions)
    {
    }

    static EventStream<Event> BuildPollingEventStream(IServiceProvider provider, Func<Task<long>> getPositionToStartFrom, TimeSpan delayOnError)
    {
        var streamScope = provider.CreateScope();

        var eventReader = streamScope.ServiceProvider.GetRequiredService<IEventStore>();
        var wakeUp = provider.GetRequiredService<WakeUp>();

        var events = PollingObservable.Poll(
            getPositionToStartFrom,
            l =>  eventReader.ReadEvents(l),
            delayOnError,
            wakeUp,
            provider.GetService<ILogger<EventStream<Event>>>()
        );

        return new(events, streamScope);
    }
}