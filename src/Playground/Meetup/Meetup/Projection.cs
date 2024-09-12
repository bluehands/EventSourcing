using System.Reactive.Linq;
using System.Reactive.Subjects;
using EventSourcing;
using EventSourcing.Infrastructure;

namespace Meetup;

public abstract class Projection<T, TEvent> : IDisposable, IInitializer<BeforeEventReplay> where T : notnull
{
    readonly IConnectableObservable<(T state, TEvent)> _connectableObservable;
    IDisposable? _connection;
    public IObservable<(T state, TEvent @event)> Changes => _connectableObservable;
    public T Current { get; private set; }

    protected Projection(IObservable<TEvent> eventStream, T initialState, Func<T, TEvent, T> apply)
    {
        Current = initialState;
        _connectableObservable = eventStream
            .Scan((state: initialState, (TEvent)default!), (state, @event) =>
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

    //public IObservable<Event<CommandProcessed>> ProcessedCommands => Changes
    //    .Select(c => c.@event)
    //    .OfType<Event<CommandProcessed>>();
}

public abstract class Projection<T>(IObservable<Event> eventStream, T initialState, Func<T, Event, T> apply)
    : Projection<T, Event>(eventStream, initialState, apply)
    where T : notnull;