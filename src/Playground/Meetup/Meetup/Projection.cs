using System.Collections.Immutable;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using EventSourcing;
using EventSourcing.Funicular.Commands;
using EventSourcing.Infrastructure;

namespace Meetup;

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

            var isOnWaitList = Attendees.Count > MaxAttendees;
            return this with
            {
                Attendees = Attendees.Add(new(registered.Name, isOnWaitList)),
                BookedUpWithin = bookedUpWithin
            };
        }
    }

    public record Attendee(string Name, bool OnWaitList);

    static State Apply(State current, Event @event)
    {
        switch (@event.Payload)
        {
            case UserGroupTalkAdded added:
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
            case AttendeeRegistered registered when current.Talks.TryGetValue(registered.TalkId, out var talk):
            {
                var updatedTalk = talk.ParticipantRegistered(registered);
                return SetTalk(current, updatedTalk);
            }
            default:
                return current;
        }
    }

    static State SetTalk(State current, Talk updatedTalk) => new (current.Talks.SetItem(updatedTalk.Id, updatedTalk));
}

public abstract class Projection<T> : IDisposable, IInitializer<BeforeEventReplay> where T : notnull
{
    readonly IConnectableObservable<(T state, Event)> _connectableObservable;
    IDisposable? _connection;
    public IObservable<(T state, Event @event)> Changes => _connectableObservable;
    public T Current { get; private set; }

    public IObservable<Event<CommandProcessed>> ProcessedCommands => Changes
        .Select(c => c.@event)
        .OfType<Event<CommandProcessed>>();

    protected Projection(IObservable<Event> eventStream, T initialState, Func<T, Event, T> apply)
    {
        Current = initialState;
        _connectableObservable = eventStream
            .Scan((state: initialState, (Event)null!), (state, @event) =>
            {
                var stateBeforeApply = state.state;
                try
                {
                    var updated = apply(stateBeforeApply, @event);
                    Current = updated;
                    return (updated, @event);
                }
                catch (Exception)
                {
                    return (state: stateBeforeApply, @event);
                }
            })
            .Publish();
    }

    public virtual void Connect() => _connection = _connectableObservable.Connect();

    public void Dispose() => _connection?.Dispose();

    public Task Initialize()
    {
        Connect();
        return Task.CompletedTask;
    }
}