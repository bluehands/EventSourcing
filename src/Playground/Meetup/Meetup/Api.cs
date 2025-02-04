using EventSourcing.Funicular.Commands;
using EventSourcing;
using System.Reactive.Linq;
using FunicularSwitch;

namespace Meetup;

public class Mutation(ICommandBus commandStream, TalksProjection talks)
{
    public async Task<Talk> NewUserGroupTalk(string title, int maxAttendees)
    {
        var newUserGroupTalk = new NewUserGroupTalk(title, maxAttendees);
        await commandStream.SendCommandAndWaitUntilApplied(newUserGroupTalk, talks.ProcessedCommands);
        return talks.Current.TalksById[newUserGroupTalk.TalkId];
    }

    public async Task<string> RegisterParticipant(string talkId, string name, string mailAddress)
    {
        var result = await commandStream.SendCommandAndWaitUntilApplied(new RegisterAttendee(talkId, name, mailAddress), talks.ProcessedCommands);
        return result.Match(_ => $"{name} registered", error: e => throw new GraphQLException(e.ToString()));
    }
}

public class Query
{
    public IQueryable<Talk> GetTalks([Service]TalksProjection talks) => talks.Current.TalksById.Values.AsQueryable();
}

public class Subscription
{
	[Subscribe(With = nameof(SubscribeToTalkChanges))]
	public TalkChanged OnTalkChanged([EventMessage] TalkChanged talk) => talk;

	public IObservable<TalkChanged> SubscribeToTalkChanges(
		[Service] TalksProjection talks,
		CancellationToken cancellationToken) => talks.Changes.SelectMany(
			c => ToTalkChange(c));

	static Option<TalkChanged> ToTalkChange((Talks state, Event @event) c)
	{
		if (c.@event.Payload is not ITalkEvent talkEvent) return Option.None<TalkChanged>();

		return c.state.TalksById.TryGetValue(talkEvent.TalkId, out var talk) 
			? new(talk, c.@event.Payload.GetType().Name) 
			: new TalkChanged(null, "Deleted");
	}

	public record TalkChanged(Talk? Talk, string ChangeInfo);
}