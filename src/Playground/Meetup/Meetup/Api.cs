using System.Reactive.Linq;
using EventSourcing;

namespace Meetup;

public class Mutation
{
    public async Task<string> NewUserGroupTalk([Service] IEventStore eventStore, [Service]TalksProjection talks, string title, int maxAttendees)
    {
        if (talks.Current.Talks.Values.Any(t => t.Title == title))
            throw new GraphQLException($"Talk with title {title} already exists");

        
        await eventStore.WriteEvents([new UserGroupTalkAdded(Guid.NewGuid().ToString(), title, maxAttendees, talks.Current.Version)]);
        return "Talk added";
    }

    public async Task<string> UpdateDescription([Service] IEventStore eventStore, [Service]TalksProjection talks, string talkId, string title)
    {
        if (!talks.Current.Talks.TryGetValue(talkId, out var talk))
            throw new GraphQLException($"Talk {talkId} does not exist");

        await eventStore.WriteEvents([new UserGroupTalkDescriptionUpdated(talkId, title)]);
        return $"Description of talk '{talk.Title}' updated";
    }

    public async Task<string> RegisterAttendee([Service] IEventStore eventStore, [Service]TalksProjection talks, string talkId, string attendeeName, string mailAddress)
    {
        if (!talks.Current.Talks.TryGetValue(talkId, out var talk))
            throw new GraphQLException($"Talk {talkId} does not exist");

        await eventStore.WriteEvents([new AttendeeRegistered(talkId, attendeeName, mailAddress, DateTimeOffset.Now)]);
        return $"{attendeeName} registered for talk '{talk.Title}'";
    }
}

public class Query
{
    public IQueryable<Talk> GetTalks([Service] TalksProjection talks) => talks.Current.Talks.Values.AsQueryable();

    public IQueryable<ChangeLog.Entry> GetChangeLog([Service] ChangeLog changeLog) => changeLog.Current.AsQueryable();

    public async Task<IQueryable<EventInfo>> GetEvents([Service] IEventStore eventStore) =>
        (await eventStore.ReadEvents().Select(MapToEventInfo).ToListAsync()).AsQueryable();

    public static EventInfo MapToEventInfo(Event e) => new(e.StreamId.StreamType, e.StreamId.Id, e.Position, e.Timestamp, e.Type, e.Payload.ToString()!);
}

public record EventInfo(string StreamType, string StreamId, long Position, DateTimeOffset Timestamp, string Type, string PayloadInfo);

public class Subscription
{
    [Subscribe(With = nameof(SubscribeToEvents))]
    public IEnumerable<Talk> OnEventReceived([EventMessage] IEnumerable<Talk> eventInfo) => eventInfo;

    public IObservable<IEnumerable<Talk>> SubscribeToEvents(
        [Service] TalksProjection talks,
        CancellationToken cancellationToken) => talks.Changes.Select(c => c.state.Talks.Values);

}