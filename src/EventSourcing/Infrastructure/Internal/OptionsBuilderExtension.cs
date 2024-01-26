﻿using System;

namespace EventSourcing.Infrastructure.Internal;

static class OptionsBuilderExtension
{
    public static TExtension GetOrCreateExtension<TExtension>(this EventSourcingOptionsBuilder optionsBuilder) where TExtension : new() =>
        optionsBuilder.Options.FindExtension<TExtension>() ??
        new TExtension();

    public static TBuilder WithOption<TBuilder, TExtension>(this TBuilder builder, Func<TExtension, TExtension> modify) where TExtension : class, IEventSourcingOptionsExtension, new()
        where TBuilder : IAllowPollingEventStreamBuilder, IEventSourcingExtensionsBuilderInfrastructure
    {
        var options = modify(builder.OptionsBuilder.GetOrCreateExtension<TExtension>());
        ((IEventSourcingOptionsBuilderInfrastructure)builder.OptionsBuilder).AddOrUpdateExtension(options);
        return builder;
    }
}