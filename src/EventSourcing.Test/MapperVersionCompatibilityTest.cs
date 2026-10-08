using EventSourcing.Infrastructure.Internal;
using EventSourcing.Persistence.EntityFramework;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using DbEvent = EventSourcing.Persistence.EntityFramework.Event;

namespace EventSourcing.Test;

[TestClass]
public class MapperVersionCompatibilityTest
{
    [TestMethod]
    public async Task MultipleStoredVersionsReplayAsTheSameDomainPayload()
    {
        var services = new ServiceCollection()
            .AddEventSourcing(options => options.UseInMemoryEventStore());
        services.RemoveAll<EventPayloadMapper>();
        services.AddSingleton<EventPayloadMapper>(new VersionOneMapper());
        services.AddSingleton<EventPayloadMapper>(new VersionTwoMapper());

        using var provider = services.BuildServiceProvider();
        await provider.StartEventSourcing();

        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EventStoreContext>();
        var timestamp = DateTimeOffset.UtcNow;
        context.Events.Add(new DbEvent(0, "Journal", "A", "VersionedEntry.v1", "{\"Text\":\"old\"}", timestamp));
        await context.SaveChangesAsync();
        context.Events.Add(new DbEvent(0, "Journal", "A", "VersionedEntry.v2", "{\"Content\":\"new\"}", timestamp));
        await context.SaveChangesAsync();

        var events = new List<Event>();
        await foreach (var @event in scope.ServiceProvider.GetRequiredService<IEventStore>().ReadEvents(0))
            events.Add(@event);

        events.Should().HaveCount(2);
        var first = events[0].Should().BeOfType<Event<VersionedEntry>>().Subject;
        var second = events[1].Should().BeOfType<Event<VersionedEntry>>().Subject;
        first.Payload.Should().Be(new VersionedEntry(new("Journal", "A"), "old"));
        second.Payload.Should().Be(new VersionedEntry(new("Journal", "A"), "new"));
        first.Position.Should().BeLessThan(second.Position);
        first.Timestamp.Should().Be(timestamp);
        second.Timestamp.Should().Be(timestamp);
    }

    [TestMethod]
    public void DuplicateStoredEventTypesAreStillRejected()
    {
        Action createMappers = () => _ = new EventPayloadMappers([new VersionOneMapper(), new VersionOneMapper()]);
        createMappers.Should().Throw<ArgumentException>();
    }

    internal record VersionedEntry(StreamId Stream, string Text) : EventPayload(Stream, "VersionedEntry.v2");

    [SerializableEventPayload("VersionedEntry.v1")]
    public record StoredVersionOne(string Text);

    [SerializableEventPayload("VersionedEntry.v2")]
    public record StoredVersionTwo(string Content);

    sealed class VersionOneMapper : EventPayloadMapper<VersionedEntry, StoredVersionOne>
    {
        protected override VersionedEntry MapFromSerializablePayload(StoredVersionOne serialized, StreamId streamId) =>
            new(streamId, serialized.Text);

        protected override StoredVersionOne MapToSerializablePayload(VersionedEntry payload) => new(payload.Text);
    }

    sealed class VersionTwoMapper : EventPayloadMapper<VersionedEntry, StoredVersionTwo>
    {
        protected override VersionedEntry MapFromSerializablePayload(StoredVersionTwo serialized, StreamId streamId) =>
            new(streamId, serialized.Content);

        protected override StoredVersionTwo MapToSerializablePayload(VersionedEntry payload) => new(payload.Text);
    }
}
