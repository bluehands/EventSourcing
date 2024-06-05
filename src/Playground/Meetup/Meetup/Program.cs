using EventSourcing;
using EventSourcing.Infrastructure.Internal;

namespace Meetup;

public static class Program
{
	public static async Task Main(string[] args)
	{
		var builder = WebApplication.CreateBuilder(args);
		var services = builder.Services;
		
		//services.AddLogging(l => l.AddConsole());

		services.AddEventSourcing(es =>
        {
            es
                //.UseSqlServerEventStore(@"Data Source=BALLARD;Initial Catalog=Meetup;Integrated Security=True;TrustServerCertificate=True")
                .UseSqliteEventStore(@"Data Source=.\EventStore.db")
                .UseFunicularCommands();
        });

		services.AddGraphQLApi();

		services.AddSingleton<Talks>()
			.AddSingleton<IInitializer>(sp => sp.GetRequiredService<Talks>());

		var app = builder.Build();

		app
			.UseRouting()
			.UseWebSockets()
			.UseEndpoints(endpoints => endpoints.MapGraphQL());

		await app.Services.StartEventSourcing();

		await app.RunAsync();
	}

    static IServiceCollection AddGraphQLApi(this IServiceCollection services)
    {
        services.AddGraphQLServer()
            .AddQueryType<Query>()
            .AddMutationType<Mutation>()
            .AddSubscriptionType<Subscription>();

        return services;
    }
}