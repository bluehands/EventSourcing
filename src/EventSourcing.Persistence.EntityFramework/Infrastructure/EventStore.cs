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
        try
        {
            await eventStore.Events.AddRangeAsync(payloads);
            await eventStore.SaveChangesAsync();
        }
        catch
        {
            // This context is dedicated to event persistence: abandon all pending changes
            // so a later write cannot implicitly retry a failed operation.
            var pendingEntries = eventStore.ChangeTracker.Entries()
                .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                .ToArray();
            foreach (var entry in pendingEntries)
                entry.State = EntityState.Detached;
            throw;
        }
    }
}
