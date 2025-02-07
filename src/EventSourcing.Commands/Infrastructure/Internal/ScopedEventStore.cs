using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace EventSourcing.Commands.Infrastructure.Internal;

public class ScopedEventStore(IEventStore eventStore, IServiceScope scope) : IEventStore, IDisposable
{
    public void Dispose()
    {
        scope.Dispose();
    }

    public IAsyncEnumerable<Event> ReadEvents(long? fromPositionInclusive = null) => eventStore.ReadEvents(fromPositionInclusive);

    public IAsyncEnumerable<Event> ReadEvents(StreamId streamId, long? fromPositionInclusive = null) => eventStore.ReadEvents(streamId, fromPositionInclusive);

    public Task WriteEvents(IReadOnlyCollection<IEventPayload> payloads) => eventStore.WriteEvents(payloads);
}