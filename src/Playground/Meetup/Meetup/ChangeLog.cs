using System.Collections.Immutable;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using EventSourcing.Infrastructure;
using Util.DiffGenerator;

namespace Meetup;

public class ChangeLog : IBeforeEventReplayInitializer, IDisposable
{
    readonly IConnectableObservable<ImmutableList<Entry>> _changes;
    IDisposable? _subscription;

    public record Entry(DateTimeOffset Timestamp, ImmutableList<string> ChangeInfos);

    public ChangeLog(TalksProjection talksProjection)
    {
        _changes = talksProjection.Changes
            .CombineWithPrevious((previous, current) =>
            {
                var diff = DiffExtensions.Diff(previous.state, current.state);
                if (diff.IsEmpty)
                    return null;

                return new Entry(current.@event.Timestamp,
                    diff.Select(d => $"{d.PropertyPath}: {d.OldValue} -> {d.NewValue}").ToImmutableList());
            })
            .Scan(ImmutableList<Entry>.Empty, (list, entry) => entry == null ? list : list.Add(entry))
            .Do(list => Current = list)
            .Publish();
    }

    public ImmutableList<Entry> Current { get; private set; }

    public Task Initialize()
    {
        _subscription = _changes.Connect();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _subscription?.Dispose();
    }
}

[Diff(typeof(TalksProjection.State))]
public static partial class DiffExtensions
{
}

public static class ObservableExtensions
{
    public static IObservable<TResult> CombineWithPrevious<TSource,TResult>(
        this IObservable<TSource> source,
        Func<TSource?, TSource, TResult> resultSelector)
    {
        return source.Scan(
                (previous: default(TSource?), current: default(TSource?)),
                (state, current) => (state.current, current))
            .Select(t => resultSelector(t.previous, t.current!));
    }
}