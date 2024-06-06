using System.Collections.Generic;
using System.Threading.Tasks;

namespace EventSourcing;

public interface IReadOnlyEventStore
{
    IAsyncEnumerable<Event> ReadEvents(long? fromPositionInclusive = null);
    IAsyncEnumerable<Event> ReadEvents(StreamId streamId, long? fromPositionInclusive = null);
}

public interface IEventStore : IReadOnlyEventStore
{
    Task WriteEvents(IReadOnlyCollection<IEventPayload> payloads);
}