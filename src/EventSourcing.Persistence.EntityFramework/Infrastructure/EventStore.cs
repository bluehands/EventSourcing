using EventSourcing.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace EventSourcing.Persistence.EntityFramework.Infrastructure;

class EventStore(EventStoreContext eventStore) : IEventReader<Event>, IEventWriter<Event>
{
    public IAsyncEnumerable<Event> ReadEvents(StreamId streamId, long? fromPositionInclusive)
    {
        var positionInclusive = fromPositionInclusive ?? 0L;

        return eventStore.Events
            .Where(e => e.StreamType == streamId.StreamType && e.StreamId == streamId.Id && e.Position >= positionInclusive)
            .AsAsyncEnumerable();
    }

    public IAsyncEnumerable<Event> ReadEvents(long? fromPositionInclusive)
    {
        var positionInclusive = fromPositionInclusive ?? 0L;

        return eventStore.Events
            .Where(e => e.Position >= positionInclusive)
            .AsAsyncEnumerable();
    }

    public async Task WriteEvents(IEnumerable<Event> payloads)
    {
        await eventStore.Events.AddRangeAsync(payloads);
        await eventStore.SaveChangesAsync();
    }
}