using System.Reactive.Linq;
using System.Reactive.Subjects;
using EventSourcing.Commands;
using FluentAssertions;

namespace EventSourcing.Test;

[TestClass]
public class CommandWaitTimeoutTest
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task CompletionDuringSendIsObservedAndSubscriptionIsReleased(bool untyped)
    {
        using var events = new Subject<Event<CommandProcessed<string>>>();
        var command = new TestCommand();
        var expected = Processed(command);
        var bus = new StubBus(_ =>
        {
	        // ReSharper disable AccessToDisposedClosure
	        events.HasObservers.Should().BeTrue("subscription must precede sending");
	        events.OnNext(Processed(new TestCommand()));
            events.OnNext(expected);
            // ReSharper restore AccessToDisposedClosure
			return Task.CompletedTask;
        });

        var result = await Wait(bus, command, events, untyped, TimeSpan.FromSeconds(5));

        result.Should().BeSameAs(expected);
        events.HasObservers.Should().BeFalse();
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task TimeoutReleasesSubscriptionAndLaterWaitStillWorks(bool untyped)
    {
        using var events = new Subject<Event<CommandProcessed<string>>>();
        var command = new TestCommand();
        var bus = new StubBus(_ => Task.CompletedTask);
        var wait = Wait(bus, command, events, untyped, TimeSpan.FromMilliseconds(100));
        events.HasObservers.Should().BeTrue();

        await Assert.ThrowsExactlyAsync<TimeoutException>(() => wait.WaitAsync(TimeSpan.FromSeconds(5)));
        events.HasObservers.Should().BeFalse();

        // Completion can still arrive after the caller timed out; it must not revive the old subscription.
        events.OnNext(Processed(command));
        var nextCommand = new TestCommand();
        var nextBus = new StubBus(sent =>
        {
	        // ReSharper disable once AccessToDisposedClosure
	        events.OnNext(Processed(sent));
            return Task.CompletedTask;
        });
        var result = await Wait(nextBus, nextCommand, events, untyped, TimeSpan.FromSeconds(5));
        result.Payload.CommandId.Should().Be(nextCommand.Id);
        events.HasObservers.Should().BeFalse();
    }

    [TestMethod]
    public async Task TimedSendFailurePropagatesAndReleasesSubscription()
    {
        using var events = new Subject<Event<CommandProcessed<string>>>();
        var failure = new InvalidOperationException("Send failed");
        var bus = new StubBus(_ => Task.FromException(failure));
        var command = new TestCommand();
        var wait = bus.SendAndWaitForProcessedEvent(command, events, TimeSpan.FromSeconds(5));

        var actual = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => wait);
        actual.Should().BeSameAs(failure);
        events.HasObservers.Should().BeFalse();
    }

    [TestMethod]
    public async Task TimeoutAlsoBoundsAnIncompleteSendWithoutCancellingIt()
    {
        using var events = new Subject<Event<CommandProcessed<string>>>();
        var send = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var bus = new StubBus(_ => send.Task);
        var wait = bus.SendAndWaitForProcessedEvent(new TestCommand(), events, TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsExactlyAsync<TimeoutException>(() => wait.WaitAsync(TimeSpan.FromSeconds(5)));
        events.HasObservers.Should().BeFalse();
        send.Task.IsCompleted.Should().BeFalse();
        send.SetResult();
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task CallerContinuationCanWaitForPublisherToReturnFromOnNext(bool withTimeout)
    {
        using var events = new Subject<Event<CommandProcessed<string>>>();
        var command = new TestCommand();
        var expected = Processed(command);
        var bus = new StubBus(_ => Task.CompletedTask);
        using var publisherReturned = new ManualResetEventSlim();
        var wait = withTimeout
            ? bus.SendAndWaitForProcessedEvent(command, events, TimeSpan.FromSeconds(10))
            : bus.SendAndWaitForProcessedEvent(command, events);
        var publisherThreadId = 0;

        // Model a sync/async caller which synchronously waits for work completed after publication.
        // Without scheduled ToTask completion, this continuation can run inside the publisher's OnNext.
        var caller = wait.ContinueWith(completed =>
        {
            var result = completed.GetAwaiter().GetResult();
            var callerThreadId = Environment.CurrentManagedThreadId;
            // ReSharper disable once AccessToDisposedClosure
            var publisherWasAbleToReturn = publisherReturned.Wait(TimeSpan.FromSeconds(2));
            return (result, callerThreadId, publisherWasAbleToReturn);
        }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        var publication = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var publisher = new Thread(() =>
        {
            publisherThreadId = Environment.CurrentManagedThreadId;
            try
            {
	            // ReSharper disable AccessToDisposedClosure
	            events.OnNext(expected);
	            publisherReturned.Set();
	            // ReSharper restore AccessToDisposedClosure
				publication.SetResult();
            }
            catch (Exception error)
            {
                publication.SetException(error);
            }
        }) { IsBackground = true };

        publisher.Start();
        try
        {
            var outcome = await caller.WaitAsync(TimeSpan.FromSeconds(10));
            await publication.Task.WaitAsync(TimeSpan.FromSeconds(5));
            outcome.result.Should().BeSameAs(expected);
            outcome.publisherWasAbleToReturn.Should().BeTrue(
                "the caller must not block OnNext while waiting for the publisher to finish");
            outcome.callerThreadId.Should().NotBe(publisherThreadId);
            events.HasObservers.Should().BeFalse();
        }
        finally
        {
            publisherReturned.Set();
            publisher.Join(TimeSpan.FromSeconds(5)).Should().BeTrue();
        }
    }

    static Task<Event<CommandProcessed<string>>> Wait(ICommandBus bus, Command command,
        IObservable<Event<CommandProcessed<string>>> events, bool untyped, TimeSpan timeout) => untyped
        ? bus.SendAndWaitForProcessedEvent<string>(command, events.Select(e => (Event)e), timeout)
        : bus.SendAndWaitForProcessedEvent(command, events, timeout);

    static Event<CommandProcessed<string>> Processed(Command command) => new(1, DateTimeOffset.UtcNow,
        new CommandProcessed<string>(CommandResult<string>.Processed(command.Id, FunctionalResult<string>.Ok("Done"))));

    record TestCommand : Command;

    sealed class StubBus(Func<Command, Task> send) : ICommandBus
    {
        public Task SendCommand(Command command) => send(command);
    }
}
