using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace EventSourcing.Infrastructure.Internal;

public class EventStreamStarter(EventStream<Event>? eventStream = null, ILogger<EventStreamStarter>? logger = null) : IInitializer<EventReplayStarting>
{
    public Task Initialize()
    {
        logger?.LogInformation("Starting event stream.");
        eventStream?.Start();
        return Task.CompletedTask;
    }
}