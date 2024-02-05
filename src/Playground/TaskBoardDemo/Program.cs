using EventSourcing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TaskBoardDemo;

class Program
{
    static async Task Main()
    {
        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(serviceCollection =>
            {
                serviceCollection
                    .AddEventSourcing(options => options.UseSqliteEventStore(@"Data Source=c:\temp\EventStore.db"));
                serviceCollection.AddSingleton<Tester>();

            })
            .ConfigureLogging(l => l.AddConsole())
            .Build();

        await host.Services.StartEventSourcing();

        await host.StartAsync();

        var tester = host.Services.GetRequiredService<Tester>();
        await tester.Run();

        await host.WaitForShutdownAsync();
    }
}

public class Tester(IEventStore store, IObservable<Event> eventStream)
{
    public async Task Run()
    {
        eventStream
            .Subscribe(e => Console.WriteLine($"Received event: {e.Position}: {e.Payload}"));

        //var allEvents = await store.ReadEvents(0).ToListAsync();

        //foreach (var @event in allEvents)
        //{
        //    Console.WriteLine($"Read event: {@event.Position}: {@event.Payload}");
        //}

        await store.WriteEvents([new TaskAdded("MyTask", "Trifft sich vorbereiten")]);
    }
}

public static class EventTypes
{
    public const string TaskAdded = "TaskAdded";
}

public static class StreamIds
{
    public static readonly StreamId Task = new("Tasks", "Tasks");
}


public record TaskAdded(string Name, string Title) : EventPayload(StreamIds.Task, EventTypes.TaskAdded);