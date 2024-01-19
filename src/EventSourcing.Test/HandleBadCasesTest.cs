using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using EventSourcing.Infrastructure;
using FluentAssertions;

namespace EventSourcing.Test;

[TestClass]
public class HandleBadCasesTest
{
    [TestMethod]
    public async Task SkipCorruptedEvent()
    {
        var test = await TestHelper.SetupEventSourcing<TestService>();

        var receivedEvents = await test.SendAndWait(new EventPayload[]
        {
            new UnreadableEntryAdded("1", "Next entry"),
            new EntryAdded("1", "Hallo diary")
        }, 1);

        receivedEvents[0].Payload.Should().BeOfType<EntryAdded>();
    }

    [TestMethod]
    public async Task MapToErrorEvent()
    {
        var test = await TestHelper.SetupEventSourcing<TestService>(options => options
            .WithCorruptedEventsHandler<MapToErrorPayloadHandler>()
        );

        var receivedEvents = await test
            .SendAndWait(new EventPayload[]
            {
                new EntryAdded("1", "Hallo diary"),
                new UnreadableEntryAdded("1", "Next entry")
            });

        receivedEvents[1].Payload.Should().BeOfType<MapToErrorPayloadHandler.CorruptedEvent>();
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

        public async Task<IList<Event>> SendAndWait(IReadOnlyCollection<EventPayload> payloads, int? numberOfExpectedEvents = null)
        {
            await _eventStore.WriteEvents(payloads);

            return await _eventStream.Take(numberOfExpectedEvents ?? payloads.Count)
                .ToList()
                .ToTask()
                .WaitAsync(TimeSpan.FromSeconds(1));
        }
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

public class MapToErrorPayloadHandler : ICorruptedEventHandler
{
    public EventPayload OnDeserializeOrMappingError(Exception error, long eventPosition, string eventType,
        DateTimeOffset timestamp, object serializedPayload) =>
        new CorruptedEvent(eventType, serializedPayload);

    public record CorruptedEvent(string EventType, object SerializedPayload) : EventPayload(new("Corrupted", "Events"), EventType);
}