using EventSourcing.Funicular.Commands;
using EventSourcing;
using System.Reactive.Linq;
using FunicularSwitch;

namespace Meetup;

public class Mutation(ICommandBus commandStream, Talks talks)
{
    public async Task<Talks.Talk> NewUserGroupTalk(string title, int maxAttendees)
    {
        var newUserGroupTalk = new NewUserGroupTalk(title, maxAttendees);
        await commandStream.SendCommandAndWaitUntilApplied(newUserGroupTalk, talks.ProcessedCommands);
        return talks.Current.Talks[newUserGroupTalk.TalkId];
    }

    public async Task<string> RegisterParticipant(string talkId, string name, string mailAddress)
    {
        var result = await commandStream.SendCommandAndWaitUntilApplied(new RegisterParticipant(talkId, name, mailAddress), talks.ProcessedCommands);
        return result.Match(_ => $"{name} registered", error: e => throw new GraphQLException(e.Message));
    }
}

public class Query
{
    public IQueryable<Talks.Talk> GetTalks([Service]Talks talks) => talks.Current.Talks.Values.AsQueryable();
}

public class Subscription
{
	[Subscribe(With = nameof(SubscribeToTalkChanges))]
	public TalkChanged OnTalkChanged([EventMessage] TalkChanged talk) => talk;

	public IObservable<TalkChanged> SubscribeToTalkChanges(
		[Service] Talks talks,
		CancellationToken cancellationToken) => talks.Changes.SelectMany(
			c => ToTalkChange(c));

	static Option<TalkChanged> ToTalkChange((Talks.State state, Event @event) c)
	{
		if (c.@event.Payload is not ITalkEvent talkEvent) return Option.None<TalkChanged>();

		return c.state.Talks.TryGetValue(talkEvent.TalkId, out var talk) 
			? new(talk, c.@event.GetType().Name) 
			: new TalkChanged(null, "Deleted");
	}

	public record TalkChanged(Talks.Talk? Talk, string ChangeInfo);
}