using System.Diagnostics;
using System.Reactive.Linq;
using EventSourcing;
using EventSourcing.Funicular.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PersistenceTester;

class Program
{
    static async Task Main(string[] args)
    {
        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(serviceCollection =>
            {
                //serviceCollection.AddEventSourcing(b => b.UseSqliteEventStore(@"Data Source=c:\temp\EventStore.db"));

                serviceCollection.AddEventSourcing(
                    eventSourcing =>
                        eventSourcing
                            .UseSqlServerEventStore("Data Source=.\\SQLSERVEREXPRESS;Initial Catalog=TestEventStore2;Integrated Security=True;TrustServerCertificate=True;")
                            .UseFunicularCommands()
                );
            })
            .ConfigureLogging(builder => builder.AddConsole())
            .Build();

        await host.StartAsync();

        await TryIt(host.Services);

        await host.WaitForShutdownAsync();
    }

    static async Task TryIt(IServiceProvider services)
    {
        var eventStream = services.GetRequiredService<IObservable<Event>>();
        using var subscription = eventStream.Subscribe(@event =>
        {
            if (@event.Position % 1000 == 0)
                Console.WriteLine($"Received: {@event.Type} ({@event.Position})");
        });

        await services.StartEventSourcing();

        var commandStream = services.GetRequiredService<CommandStream>();
        await commandStream.SendCommandAndWaitUntilApplied(new AddTextCommand("Hallo"),  eventStream.Select(e => e.Payload).OfType<CommandProcessed>());
        
        var eventStore = services.GetRequiredService<IEventStore>();

        Console.WriteLine("Reading all events from store...");
        await foreach (var @event in eventStore.ReadEvents(0))
        {
            if (@event.Position % 1000 == 0)
                Console.WriteLine($"Read {@event.Position} events from store");
        }

        var cancellationToken = services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                try
                {
                    var events = Enumerable.Range(0, 1000).Select(i => 
                            new TextAdded("Journal1",$"Batch {i}", TextGenerator.RandomString(10, 1000)))
                        .ToList();
                    var sw = Stopwatch.StartNew();
                    await eventStore.WriteEvents(events);
                    Console.WriteLine($"Wrote {events.Count} events in {sw.ElapsedMilliseconds} ms");
                }
                catch (Exception)
                {
                    Console.WriteLine("Failed to write event");
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }
}

public static class TextGenerator
{
    static readonly Random Random = new();

    public static string RandomString(int minLength, int maxLength) => RandomString(Random.Next(minLength, maxLength));

    public static string RandomString(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        return new string(Enumerable.Repeat(chars, length)
            .Select(s => s[Random.Next(s.Length)]).ToArray());
    }
}

public static class EventTypes
{
    public const string TextAdded = "TextAdded";
}

public record AddTextCommand(string Text) : Command;

public class AddTextCommandProcessor : SynchronousCommandProcessor<AddTextCommand>
{
    public override CommandResult.Processed_ InternalProcessSync(AddTextCommand command)
    {


        return new(new TextAdded("MyJournal", "First entry", command.Text), command.Id,
            FunctionalResult.Ok("Text added"));
    }
}

public record TextAdded(string JournalId, string Header, string Text) : EventPayload(new("Journal", JournalId), EventTypes.TextAdded);

[SerializableEventPayload(EventTypes.TextAdded)]
public record TextAddedSerializable(string HeaderProp, string TextProp);

public class TextAddedPayloadMapper : EventPayloadMapper<TextAdded, TextAddedSerializable>
{
    protected override TextAdded MapFromSerializablePayload(TextAddedSerializable serialized, StreamId streamId) => new(streamId.Id, serialized.HeaderProp, serialized.TextProp);

    protected override TextAddedSerializable MapToSerializablePayload(TextAdded payload) =>  new(payload.Header, payload.Text);
}