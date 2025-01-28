using System.Reactive;
using EventSourcing.Funicular.Commands.Infrastructure.Internal;
using EventSourcing.Infrastructure.Internal;
using Microsoft.Extensions.Logging;

namespace EventSourcing.Funicular.Commands.Defaults.Infrastructure.Internal;

public static class CommandRegistrationExtensions
{
    public static IDisposable SubscribeCommandProcessors(
        this IObservable<Command> commands,
        GetCommandProcessor<Failure> getCommandProcessor,
        Func<ScopedEventStore> getEventStore,
        ILogger? logger,
        WakeUp? eventPollWakeUp)
        => commands.SubscribeCommandProcessors<Failure, OperationResult<Unit>>(
            getCommandProcessor, getEventStore, logger, eventPollWakeUp);
}