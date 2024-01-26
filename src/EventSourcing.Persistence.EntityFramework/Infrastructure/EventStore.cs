using EventSourcing.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace EventSourcing.Persistence.EntityFramework.Infrastructure;

class EventStore(EventStoreContext eventStore) : IEventReader<Event>, IEventWriter<Event>
{
    public IAsyncEnumerable<Event> ReadEvents(StreamId streamId) => eventStore.Events
        .Where(e => e.StreamType == streamId.StreamType && e.StreamId == streamId.Id)
        .AsAsyncEnumerable();

    public IAsyncEnumerable<Event> ReadEvents(long fromPositionInclusive) => eventStore.Events
        .Where(e => e.Position >= fromPositionInclusive)
        .AsAsyncEnumerable();

    public async Task WriteEvents(IEnumerable<Event> payloads)
    {
        await eventStore.Events.AddRangeAsync(payloads);
        await eventStore.SaveChangesAsync();
    }
}