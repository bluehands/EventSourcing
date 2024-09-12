﻿using System.Collections.Immutable;
using EventSourcing;

namespace Meetup;

public record Talk(string Id, string Title, int MaxAttendees, ImmutableList<Attendee> Attendees, ImmutableList<Attendee> WaitList)
{
    public Talk AddAttendee(Attendee attendee) =>
        Attendees.Count < MaxAttendees
            ? this with { Attendees = Attendees.Add(attendee) }
            : this with { WaitList = WaitList.Add(attendee) };
}

public record Attendee(string Name, string MailAddress);

public class TalksProjection(IObservable<Event> eventStream)
    : Projection<TalksProjection.State>(eventStream, State.Empty, Apply)
{
    static State Apply(State current, Event @event)
    {
        var version = @event.Position;
        switch (@event.Payload)
        {
            case UserGroupTalkAdded talkAdded:
                {
                    var newTalk = new Talk(talkAdded.TalkId, talkAdded.Title, talkAdded.MaxAttendees, ImmutableList<Attendee>.Empty, ImmutableList<Attendee>.Empty);
                    return current.AddTalk(newTalk, version);
                }
            case UserGroupTalkDescriptionUpdated descriptionUpdated:
                {
                    return current.UpdateTalk(descriptionUpdated.TalkId, t => t with { Title = descriptionUpdated.Title }, version);
                }
            case AttendeeRegistered attendeeRegistered:
                {
                    return current.UpdateTalk(attendeeRegistered.TalkId, t => t.AddAttendee(new Attendee(attendeeRegistered.Name, attendeeRegistered.MailAddress)), version);
                }
            default:
                return current;
        }
    }

    public record State(ImmutableDictionary<string, Talk> Talks, long Version)
    {
        public static readonly State Empty = new(ImmutableDictionary<string, Talk>.Empty, 0);

        public State AddTalk(Talk newTalk, long version) => UpdateTalks(version, Talks.Add(newTalk.Id, newTalk));

        public State UpdateTalk(string talkId, Func<Talk, Talk> update, long version)
        {
            if (!Talks.TryGetValue(talkId, out var talk))
                return this;

            var updatedTalk = update(talk);
            if (ReferenceEquals(updatedTalk, talk))
                return this;

            return UpdateTalks(version, Talks.SetItem(talkId, updatedTalk));
        }

        static State UpdateTalks(long version, ImmutableDictionary<string, Talk> talks) => new(talks, version);
    }
}