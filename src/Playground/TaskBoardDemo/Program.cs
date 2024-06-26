﻿using EventSourcing;
using EventSourcing.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TaskBoardDemo;

class Program
{
    static async Task Main()
    {
        var host = Host.CreateDefaultBuilder()
            .ConfigureServices(serviceCollection =>
            {
                serviceCollection.AddEventSourcing(options =>
                    options.UseSqliteEventStore(@"Data Source=c:\temp\EventStore.db")
                );

                serviceCollection.AddSingleton<Tester>();
                serviceCollection.AddInitializer<BeforeReplayInitializer>();
            })
            .ConfigureLogging(l => l.AddConsole())
            .Build();

        await host.Services.StartEventSourcing();
        await Task.Delay(1000);

        await host.StartAsync();

        await host.Services.GetRequiredService<Tester>().Run();

        await host.WaitForShutdownAsync();
    }
}

public class Tester(IEventStore eventStore, IObservable<Event> eventStream)
{
    public async Task Run()
    {
        await eventStore.WriteEvents([new TaskAdded("myfirsttask", "trifft sich vorbereiten", new ("event sourcing fertig basteln", "Alex"))]);
    }

    public void SubscribeEvents() {
        eventStream.Subscribe(e => Console.WriteLine($"Received event ({e.Position}): {e.Payload}"));
    }
}

public class BeforeReplayInitializer(Tester tester) : IInitializer<BeforeEventReplay>
{
    public Task Initialize()
    {
        tester.SubscribeEvents();
        return Task.CompletedTask;
    }
}

public static class EventTypes
{
    public const string TaskAdded = "TaskAdded";
}

public static class StreamIds
{
    public static StreamId Task(string taskId) => new("Task", taskId);

}

public record TaskDescription(string Description, string Author);


public record TaskAdded : EventPayload
{
    public TaskAdded(string taskId, string name, TaskDescription description) : base(StreamIds.Task(taskId), EventTypes.TaskAdded)
    {
        Name = name;
        Description = description;
    }

    public string TaskId => StreamId.Id;
    public string Name { get;  }
    public TaskDescription Description { get; }
}