using System.Threading.Tasks;

namespace EventSourcing.Funicular.Commands.Infrastructure.Internal;

public interface IEventReplayState
{
    Task WaitForReplayDone();
}