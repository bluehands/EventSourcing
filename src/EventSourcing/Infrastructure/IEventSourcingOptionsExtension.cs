using Microsoft.Extensions.DependencyInjection;

namespace EventSourcing.Infrastructure;

public interface IEventSourcingOptionsExtension 
{
    void SetDefaults(EventSourcingOptionsBuilder builder);
    void ApplyServices(IServiceCollection serviceCollection);
    void AddDefaultServices(IServiceCollection serviceCollection);
}