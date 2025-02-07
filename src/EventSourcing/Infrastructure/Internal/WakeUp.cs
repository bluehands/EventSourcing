using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nito.AsyncEx;

namespace EventSourcing.Infrastructure.Internal;

public class WakeUp(TimeSpan minWaitTime, TimeSpan maxWaitTime, ILogger? logger)
{
	readonly AsyncManualResetEvent _resetEvent  = new(true);
	readonly TimeSpan _maxWaitTime = maxWaitTime;
    TimeSpan _currentWaitTime = maxWaitTime;

    public async Task WaitForSignalOrUntilTimeout(bool wakeMeUpSoon, CancellationToken cancellationToken)
    {
        if (wakeMeUpSoon)
            _currentWaitTime = minWaitTime;

        var timeout = Task.Delay(_currentWaitTime, cancellationToken);
        // ReSharper disable once MethodSupportsCancellation -> do not use overload with cancellation, it collects CancellationTaskTokenSource objects. Cancellation works anyway because timeout task supports cancellation, that's enough because of WhenAny
        var signal = _resetEvent.WaitAsync();
        var completedTask = await Task.WhenAny(timeout, signal).ConfigureAwait(false);
        if (completedTask == signal)
            _currentWaitTime = minWaitTime;
        else
        {
            var nextWaitTime = _currentWaitTime.Add(_currentWaitTime == TimeSpan.Zero ? TimeSpan.FromTicks(_maxWaitTime.Ticks / 10) : _currentWaitTime);
            _currentWaitTime = nextWaitTime < _maxWaitTime ? nextWaitTime : _maxWaitTime;
        }
    }

    public void WorkIsScheduled() => _resetEvent.Reset();

	public void ThereIsWorkToDo()
	{
		_resetEvent.Set();
		if (logger?.IsEnabled(LogLevel.Debug) ?? false)
			logger.LogDebug("Signaled");
	}
}