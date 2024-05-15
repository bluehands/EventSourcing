using System.Threading.Tasks;

namespace EventSourcing.Funicular.Commands;

public interface ICommandBus
{
    Task SendCommand(Command command);
}