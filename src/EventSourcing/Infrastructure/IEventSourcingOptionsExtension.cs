using Microsoft.Extensions.DependencyInjection;

namespace EventSourcing.Infrastructure;

public interface IEventSourcingOptionsExtension
{
    void ApplyServices(IServiceCollection serviceCollection);
}