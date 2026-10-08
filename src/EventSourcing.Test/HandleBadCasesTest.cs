using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using EventSourcing.Infrastructure;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using EFEvent = EventSourcing.Persistence.EntityFramework.Event;

namespace EventSourcing.Test;

public class HandleBadCasesTest
{
    [Fact]
    public async Task SkipCorruptedEvent()
    {
        var test = await TestHelper.SetupEventSourcing<TestService>();

        var receivedEvents = await test.SendAndWait([
            new UnreadableEntryAdded("1", "Next entry"),
            new EntryAdded("1", "Hallo diary")
        ], 1);

        receivedEvents[0].Payload.Should().BeOfType<EntryAdded>();
    }

    [Fact]
    public async Task MapToErrorEvent()
    {
        var test = await TestHelper.SetupEventSourcing<TestService>(options => options
            .WithCorruptedEventsHandler<MapToErrorPayloadHandler>()
        );

        var receivedEvents = await test
            .SendAndWait([
                new EntryAdded("1", "Hallo diary"),
                new UnreadableEntryAdded("1", "Next entry")
            ]);

        receivedEvents[1].Payload.Should().BeOfType<MapToErrorPayloadHandler.CorruptedEvent>();
    }

    [Fact]
    public async Task ErrorReadingEvents()
    {
        const int failTimes = 3;
        var mockEventStore = new MockEventStore(failTimes);
        var delayOnError = TimeSpan.FromMilliseconds(150);
        var test = await TestHelper.SetupEventSourcing<TestService>(
            modifyServices: services => services
                .RemoveAll<IEventReader<EFEvent>>()
                .AddSingleton<IEventReader<EFEvent>>(mockEventStore)
                .RemoveAll<IEventWriter<EFEvent>>()
                .AddSingleton<IEventWriter<EFEvent>>(mockEventStore),
            inMemoryOptions: inMemoryOptions =>
            {
                inMemoryOptions.UsePollingEventStream(
                    minWaitTime: TimeSpan.Zero,
                    maxWaitTime: TimeSpan.FromMilliseconds(50),
                    delayOnError: delayOnError);
            });

        var stopwatch = new Stopwatch();
        stopwatch.Start();

        var receivedEvents = await test
            .SendAndWait([
                new EntryAdded("1", "Hallo diary"),
            ]);

        stopwatch.Stop();
        receivedEvents.Should().HaveCount(1);
        stopwatch.Elapsed
            .Should().BeGreaterThanOrEqualTo((failTimes - 1) * delayOnError);
    }

    class StreamIds
    {
        public static StreamId Journal(string id) => new("Journal", id);
    }

    [SerializableEventPayload("EntryAdded")]
    record EntryAdded : EventPayload
    {
        public EntryAdded(string journalId, string text) : base(StreamIds.Journal(journalId), "EntryAdded")
        {
            JournalId = journalId;
            Text = text;
        }

        public string JournalId { get; }
        public string Text { get; }
    }

    [SerializableEventPayload("UnreadableEntryAdded")]
    record UnreadableEntryAdded : EventPayload //this cannot be deserialized because there is no matching property for constructor parameter journalId
    {
        public UnreadableEntryAdded(string journalId, string text) : base(StreamIds.Journal(journalId), "UnreadableEntryAdded") => Text = text;
        public string Text { get; }
    }
}

class TestService
{
    readonly EventStream<Event> _eventStream;
    readonly IEventStore _eventStore;

    public TestService(EventStream<Event> eventStream, IEventStore eventStore)
    {
        _eventStream = eventStream;
        _eventStore = eventStore;
    }

    public async Task<IList<Event>> SendAndWait(
        IReadOnlyCollection<IEventPayload> payloads,
        int? numberOfExpectedEvents = null)
    {
        await _eventStore.WriteEvents(payloads);

        return await _eventStream.Take(numberOfExpectedEvents ?? payloads.Count)
            .ToList()
            .ToTask()
            .WaitAsync(TimeSpan.FromSeconds(1));
    }
}

public class MapToErrorPayloadHandler : ICorruptedEventHandler
{
    public IEventPayload OnDeserializeOrMappingError(Exception error, long eventPosition, string eventType,
        DateTimeOffset timestamp, object serializedPayload) =>
        new CorruptedEvent(eventType, serializedPayload);

    public record CorruptedEvent(string EventType, object SerializedPayload) : EventPayload(new("Corrupted", "Events"), EventType);
}

public class MockEventStore(int failTimes) : IEventReader<EFEvent>, IEventWriter<EFEvent>
{
    private readonly ConcurrentQueue<EFEvent> events = new();
    private readonly int failTimes = failTimes;
    private int tries = 0;
    
    public async IAsyncEnumerable<EFEvent> ReadEvents(StreamId streamId, long? fromPositionInclusive)
    {
        this.tries += 1;
        if (this.tries <= this.failTimes)
        {
            throw new Exception("BOOM");
        }

        while (this.events.TryDequeue(out var @event))
        {
            if (fromPositionInclusive.HasValue && @event.Position >= fromPositionInclusive.Value && @event.StreamId == streamId.Id)
            {
                yield return @event;
            }
        }
    }

    public async IAsyncEnumerable<EFEvent> ReadEvents(long? fromPositionInclusive)
    {
        this.tries += 1;
        if (this.tries <= this.failTimes)
        {
            throw new Exception("BOOM");
        }

        while (this.events.TryDequeue(out var @event))
        {
            if (fromPositionInclusive.HasValue && @event.Position >= fromPositionInclusive.Value)
            {
                yield return @event;
            }
        }
    }

    public Task WriteEvents(IEnumerable<EFEvent> payloads)
    {
        foreach (var payload in payloads)
        {
            this.events.Enqueue(payload);
        }

        return Task.CompletedTask;
    }
}
