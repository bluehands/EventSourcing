using System.Threading.Tasks;

namespace EventSourcing.Commands.Infrastructure;

public interface IEventReplayState
{
    Task WaitForReplayDone();
}