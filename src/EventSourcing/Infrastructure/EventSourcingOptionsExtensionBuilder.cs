using System;

namespace EventSourcing.Infrastructure;

public abstract class EventSourcingOptionsExtensionBuilder<TBuilder, TExtension> : IEventSourcingExtensionsBuilderInfrastructure
    where TBuilder : EventSourcingOptionsExtensionBuilder<TBuilder, TExtension> where TExtension : class, IEventSourcingOptionsExtension, new()
{
    readonly EventSourcingOptionsBuilder _optionsBuilder;
    public EventSourcingOptionsBuilder OptionsBuilder => _optionsBuilder;

    protected EventSourcingOptionsExtensionBuilder(EventSourcingOptionsBuilder optionsBuilder)
    {
        _optionsBuilder = optionsBuilder;
        WithOption<TExtension>(e => e);
    }

    protected TBuilder WithOption(Func<TExtension, TExtension> modify) => WithOption<TExtension>(modify);

    protected TBuilder WithOption<TOptionsExtension>(Func<TOptionsExtension, TOptionsExtension> modify) where TOptionsExtension : class, IEventSourcingOptionsExtension, new()
    {
        var options = modify(GetOrCreateExtension<TOptionsExtension>());
        ((IEventSourcingOptionsBuilderInfrastructure)_optionsBuilder).AddOrUpdateExtension(options);
        return (TBuilder)this;
    }

    TOptionsExtension GetOrCreateExtension<TOptionsExtension>() where TOptionsExtension : new() =>
        _optionsBuilder.Options.FindExtension<TOptionsExtension>() ?? new TOptionsExtension();
}