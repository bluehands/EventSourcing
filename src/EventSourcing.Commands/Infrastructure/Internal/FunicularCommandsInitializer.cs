﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EventSourcing.Infrastructure;
using EventSourcing.Infrastructure.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EventSourcing.Funicular.Commands.Infrastructure.Internal;

public class FunicularEventSourcingContext(
    IEventReplayState eventReplayState,
    IServiceScopeFactory scopeFactory,
    ILogger<EventSourcingContext>? logger = null) 
    : EventSourcingContext(scopeFactory, logger)
{
    protected override async Task Initialize(Type phase, IEnumerable<IInitializer> initializers)
    {
        if (phase == typeof(AfterEventReplay))
            await eventReplayState.WaitForReplayDone(); 

        await base.Initialize(phase, initializers);
    }
}

internal sealed class FunicularCommandsInitializer<TFailure>(
    CommandProcessorSubscription<TFailure> commandProcessorSubscription,
    EventReplayState<TFailure> eventReplayState) : IInitializer<EventReplayStarted>
    where TFailure : IFailure<TFailure>
{
    public Task Initialize()
    {
        commandProcessorSubscription.SubscribeCommandProcessors();
        eventReplayState.SendNoopCommand();
        return Task.CompletedTask;
    }
}