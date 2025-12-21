using System;
using System.Collections.Generic;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace EventSourcing.Infrastructure.Internal;

public static class PollingObservable
{
    public static IObservable<Event> Poll(
        Func<Task<long>> getPositionToStartFrom,
        Func<long, IAsyncEnumerable<Event>> poll,
        TimeSpan delayOnError,
        WakeUp wakeUp,
        ILogger? logger) =>
        Observable.Create<Event>(observer => Scheduler.Default.ScheduleAsync(async (_, ct) =>
        {
            var isFirst = true;
            var lastInPackage = default(Event);
            var arg = default(long);

            while (!ct.IsCancellationRequested)
            {
                var streamIsHot = false;
                var faulted = false;
                try
                {
                    wakeUp.WorkIsScheduled();
                    var nextState = isFirst ? await getPositionToStartFrom().ConfigureAwait(false) : (lastInPackage?.Position + 1) ?? arg;
                    streamIsHot = !Equals(nextState, arg);
                    arg = nextState;
                    lastInPackage = null;
                    var result = poll(arg);
                    isFirst = false;
                    await foreach (var @event in result)
                    {
                        if (ct.IsCancellationRequested)
                            break;

                        lastInPackage = @event;
                        observer.OnNext(@event);
                    }
                }
                catch (Exception ex)
                {
                    //exception reading events from db. Retry after certain time. TODO: use policy here
                    faulted = true;
                    logger?.LogError(ex, $"Poll failed. No events will be published. Poll will be retried at position {arg}. Delay retry for {delayOnError}.");
                }
                try
                {
                    if (faulted)
                    {
                        await Task.Delay(delayOnError, ct).ConfigureAwait(false);
                    }
                    else
                    {
                        await wakeUp.WaitForSignalOrUntilTimeout(streamIsHot, ct).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }));
}