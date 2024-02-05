using System.Collections.Generic;
using System.Threading.Tasks;

namespace EventSourcing;

public interface IEventStore
{
    IAsyncEnumerable<Event> ReadEvents(long? fromPositionInclusive = null);
    IAsyncEnumerable<Event> ReadEvents(StreamId streamId, long? fromPositionInclusive = null);
    Task WriteEvents(IReadOnlyCollection<IEventPayload> payloads);
}