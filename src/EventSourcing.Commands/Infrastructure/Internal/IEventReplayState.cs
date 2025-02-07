using System.Threading.Tasks;

namespace EventSourcing.Commands.Infrastructure.Internal;

public interface IEventReplayState
{
    Task WaitForReplayDone();
}