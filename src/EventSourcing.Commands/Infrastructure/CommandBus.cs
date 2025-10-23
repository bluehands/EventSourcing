using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace EventSourcing.Commands.Infrastructure;

public class CommandBus(Internal.CommandBus commandBus, IServiceScopeFactory serviceScopeFactory) : ICommandBus
{
    public Task SendCommand(Command command) => commandBus.SendCommand(new(command, serviceScopeFactory.CreateScope()));
}