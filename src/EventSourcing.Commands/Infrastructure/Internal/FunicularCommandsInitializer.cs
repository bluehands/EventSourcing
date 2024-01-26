using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EventSourcing.Infrastructure;
using EventSourcing.Infrastructure.Internal;
using Microsoft.Extensions.Logging;

namespace EventSourcing.Funicular.Commands.Infrastructure.Internal;

public class FunicularEventSourcingContext(EventReplayState eventReplayState, IEnumerable<IInitializer> initializers, ILogger<EventSourcingContext>? logger = null) 
    : EventSourcingContext(initializers, logger)
{
    protected override async Task Initialize(Type phase, IEnumerable<IInitializer> initializers)
    {
        if (phase == typeof(AfterEventReplay))
            await eventReplayState.WaitForReplayDone(); 

        await base.Initialize(phase, initializers);
    }
}

sealed class FunicularCommandsInitializer(CommandProcessorSubscription commandProcessorSubscription, EventReplayState eventReplayState) : IInitializer<BeforeEventReplay>
{
    public Task Initialize()
    {
        commandProcessorSubscription.SubscribeCommandProcessors();
        eventReplayState.SendNoopCommand();
        return Task.CompletedTask;
    }
}