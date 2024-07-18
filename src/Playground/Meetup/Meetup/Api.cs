using System.Reactive.Linq;
using EventSourcing;

namespace Meetup;

public class Mutation
{
    public async Task<string> NewUserGroupTalk([Service] IEventStore eventStore, string title, int maxAttendees)
    {
        await eventStore.WriteEvents([new UserGroupTalkAdded(Guid.NewGuid().ToString(), title, maxAttendees)]);
        return "Talk added";
    }
}

public class Query
{
    public async Task<IQueryable<EventInfo>> GetEvents([Service] IEventStore eventStore) =>
        (await eventStore.ReadEvents().Select(MapToEventInfo).ToListAsync()).AsQueryable();

    public static EventInfo MapToEventInfo(Event e) => new(e.StreamId.StreamType, e.StreamId.Id, e.Position, e.Timestamp, e.Type, e.Payload.ToString()!);
}

public record EventInfo(string StreamType, string StreamId, long Position, DateTimeOffset Timestamp, string Type, string PayloadInfo);

public class Subscription
{
    [Subscribe(With = nameof(SubscribeToEvents))]
    public EventInfo OnEventReceived([EventMessage] EventInfo eventInfo) => eventInfo;

    public IObservable<EventInfo> SubscribeToEvents(
        [Service] IObservable<Event> eventStream,
        CancellationToken cancellationToken) => eventStream.Select(Query.MapToEventInfo);
}