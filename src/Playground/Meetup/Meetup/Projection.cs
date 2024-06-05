﻿using System.Reactive.Linq;
using System.Reactive.Subjects;
using EventSourcing;
using EventSourcing.Funicular.Commands;
using EventSourcing.Infrastructure;

namespace Meetup;

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