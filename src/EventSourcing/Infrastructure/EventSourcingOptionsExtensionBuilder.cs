using System;

namespace EventSourcing.Infrastructure;

public abstract class EventSourcingOptionsExtensionBuilder<TBuilder> : IEventSourcingExtensionsBuilderInfrastructure
    where TBuilder : EventSourcingOptionsExtensionBuilder<TBuilder>
{
    readonly EventSourcingOptionsBuilder _optionsBuilder;
    public EventSourcingOptionsBuilder OptionsBuilder => _optionsBuilder;

    protected EventSourcingOptionsExtensionBuilder(EventSourcingOptionsBuilder optionsBuilder) => _optionsBuilder = optionsBuilder;

    protected TExtension GetOrCreateExtension<TExtension>() where TExtension : new() =>
        _optionsBuilder.Options.FindExtension<TExtension>() ??
        new TExtension();

    protected TBuilder WithOption<TExtension>(Func<TExtension, TExtension> modify) where TExtension : class, IEventSourcingOptionsExtension, new()
    {
        var options = modify(GetOrCreateExtension<TExtension>());
        ((IEventSourcingOptionsBuilderInfrastructure)_optionsBuilder).AddOrUpdateExtension(options);
        return (TBuilder)this;
    }
}