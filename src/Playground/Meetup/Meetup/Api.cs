using EventSourcing.Funicular.Commands;
using EventSourcing;
using System.Collections.Immutable;
using System.Reactive.Linq;
using FunicularSwitch;

namespace Meetup;

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

public class Talks(IObservable<Event> eventStream) : Projection<Talks.State>(eventStream, new(ImmutableDictionary<string, Talk>.Empty), Apply)
{
	public record State(ImmutableDictionary<string, Talk> Talks);

	public record Talk(string Id, string Title, int MaxAttendees, ImmutableList<Attendee> Attendees, DateTimeOffset TalkPublished, TimeSpan? BookedUpWithin)
	{
		public Talk ParticipantRegistered(AttendeeRegistered registered)
		{
			var bookedUpWithin = Attendees.Count == MaxAttendees - 1
				? (registered.RegisteredAt - TalkPublished)
				: BookedUpWithin;

			var waitList = Attendees.Count > MaxAttendees;
			return this with
			{
				Attendees = Attendees.Add(new(registered.Name, waitList)),
				BookedUpWithin = bookedUpWithin
			};
		}
	}

	public record Attendee(string Name, bool OnWaitList);

	static State Apply(State current, Event @event)
	{
		if (@event.Payload is UserGroupTalkAdded added)
		{
			var updatedTalk = new Talk(
				added.TalkId,
				added.Title, 
				added.MaxAttendees,
				[],
				@event.Timestamp,
				null);
			return SetTalk(current, updatedTalk);
		}

		if (@event.Payload is AttendeeRegistered registered)
		{
			if (current.Talks.TryGetValue(registered.TalkId, out var talk))
			{
				var updatedTalk = talk.ParticipantRegistered(registered);
				return SetTalk(current, updatedTalk);
			}
		}

		return current;
	}

	static State SetTalk(State current, Talk updatedTalk) => new (current.Talks.SetItem(updatedTalk.Id, updatedTalk));
}