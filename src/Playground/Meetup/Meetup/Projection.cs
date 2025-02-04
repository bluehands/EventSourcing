using System.Collections.Immutable;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using EventSourcing;
using EventSourcing.Funicular.Commands;
using EventSourcing.Infrastructure;

namespace Meetup;

public record Attendee(string Name, bool IsOnWaitList);

public record Talk(
    string Id,
    string Title,
    int MaxAttendees,
    ImmutableList<Attendee> Attendees,
    DateTimeOffset TalkPublished,
    TimeSpan? BookedUpWithin)
{
    public Talk AttendeeRegistered(AttendeeRegistered registered)
    {
        var bookedUpWithin = Attendees.Count == MaxAttendees - 1
            ? (registered.RegisteredAt - TalkPublished)
            : BookedUpWithin;

        var isOnWaitList = Attendees.Count >= MaxAttendees;
        return this with
        {
            Attendees = Attendees.Add(new(registered.Name, isOnWaitList)),
            BookedUpWithin = bookedUpWithin
        };
    }
}

public record Talks(ImmutableDictionary<string, Talk> TalksById)
{
    public Talks SetTalk(Talk updatedTalk) => new(TalksById.SetItem(updatedTalk.Id, updatedTalk));
}

public class TalksProjection(IObservable<Event> eventStream) : Projection<Talks>(eventStream, new(ImmutableDictionary<string, Talk>.Empty), ApplyEvent)
{
    static Talks ApplyEvent(Talks current, Event @event)
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
                    return current.SetTalk(updatedTalk);
                }
            case AttendeeRegistered registered when current.TalksById.TryGetValue(registered.TalkId, out var talk):
                {
                    var updatedTalk = talk.AttendeeRegistered(registered);
                    return current.SetTalk(updatedTalk);
                }
            default:
                return current;
        }
    }
}

public abstract class Projection<T> : IDisposable, IInitializer<BeforeEventReplay> where T : notnull
{
    readonly IConnectableObservable<(T state, Event)> _connectableObservable;
    IDisposable? _connection;
    public IObservable<(T state, Event @event)> Changes => _connectableObservable;
    public T Current { get; private set; }

    public IObservable<Event<CommandProcessed<Failure>>> ProcessedCommands => Changes
        .Select(c => c.@event)
        .OfType<Event<CommandProcessed<Failure>>>();

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