using EventSourcing.Infrastructure;
using EventSourcing.Infrastructure.Internal;
using EventSourcing.Persistence.EntityFramework.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EventSourcing.Persistence.EntityFramework.Postgres.Infrastructure.Internal;

public record PostgresEventStoreOptionsExtension(Func<IServiceProvider, string?> ConnectionString) : IEventSourcingOptionsExtension
{
    public PostgresEventStoreOptionsExtension() : this(_ => null) { }

    public void SetDefaults(EventSourcingOptionsBuilder builder)
    {
        if (!builder.EventStreamOptionsConfigured())
            new PostgresEventStoreOptionsBuilder(builder).UsePollingEventStream();
    }

    public void ApplyServices(IServiceCollection services)
    {
        services.AddDbContext<PostgresEventStoreContext>((sp, options) =>
            options.UseNpgsql(ConnectionString(sp), postgres => postgres
                .MigrationsAssembly(typeof(PostgresEventStoreContext).Assembly.GetName().Name!)
                .MigrationsHistoryTable("__EFMigrationsHistory", "public")));
        services.AddScoped<EventStoreContext>(sp => sp.GetRequiredService<PostgresEventStoreContext>());
        services.AddEntityFrameworkServices();
        services.Replace(ServiceDescriptor.Transient<IEventWriter<Event>, PostgresEventWriter>());
        services.Replace(ServiceDescriptor.Transient<IDbEventDescriptor<Event, string>, PostgresEventDescriptor>());
    }

    public void AddDefaultServices(IServiceCollection services, EventSourcingOptions options) { }
}
