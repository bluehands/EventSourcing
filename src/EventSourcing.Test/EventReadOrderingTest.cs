using EventSourcing.Infrastructure.Internal;
using EventSourcing.Persistence.EntityFramework;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EventSourcing.Test;

public class EventReadOrderingTest
{
    [Theory]
    [InlineData(false, 0L)]
    [InlineData(false, 3L)]
    [InlineData(true, 0L)]
    [InlineData(true, 3L)]
    public async Task ReadersReturnEventsInAscendingPositionOrder(bool byStream, long fromPositionInclusive)
    {
        await using var services = CreateServices();
        await SeedEvents(services);
        using var scope = services.CreateScope();
        await ReverseUnorderedReads(scope);
        var store = scope.ServiceProvider.GetRequiredService<IEventStore>();

        var events = new List<Event>();
        var read = byStream
            ? store.ReadEvents(new StreamId("ReadOrdering", "A"), fromPositionInclusive)
            : store.ReadEvents(fromPositionInclusive);
        await foreach (var @event in read)
            events.Add(@event);

        var expected = Enumerable.Range(1, 5).Select(position => (long)position)
            .Where(position => position >= fromPositionInclusive && (!byStream || position % 2 == 1));
        events.Select(e => e.Position).Should().Equal(expected);
    }

    [Fact]
    public async Task PollingDeliversEventsOnceAndInOrderAcrossTwoPolls()
    {
        await using var services = CreateServices();
        await SeedEvents(services);
        using var scope = services.CreateScope();
        await ReverseUnorderedReads(scope);
        var store = scope.ServiceProvider.GetRequiredService<IEventStore>();
        var received = new List<Event>();
        var secondPollCompleted = new TaskCompletionSource<Event[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        var pollCount = 0;

        // Observe two complete real reads, including the cursor selected after the first batch.
        async IAsyncEnumerable<Event> ReadTwoPolls(long position)
        {
            var currentPoll = ++pollCount;
            if (currentPoll > 2)
                yield break;

            await foreach (var @event in store.ReadEvents(position))
                yield return @event;

            if (currentPoll == 2)
                secondPollCompleted.TrySetResult(received.ToArray());
        }

        var polling = PollingObservable.Poll(
            () => Task.FromResult(0L), ReadTwoPolls, TimeSpan.Zero,
            new WakeUp(TimeSpan.Zero, TimeSpan.FromMilliseconds(10), logger: null), logger: null);
        using var subscription = polling.Subscribe(received.Add, error => secondPollCompleted.TrySetException(error));
        var delivered = await secondPollCompleted.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        delivered.Select(e => e.Position).Should().Equal(1L, 2L, 3L, 4L, 5L);
    }

    static ServiceProvider CreateServices() => new ServiceCollection()
        .AddEventSourcing(options => options
            .UseInMemoryEventStore()
            .PayloadAssemblies(typeof(EventReadOrderingTest).Assembly))
        .BuildServiceProvider();

    static async Task SeedEvents(ServiceProvider services)
    {
        using var scope = services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<EventStoreContext>().Database.EnsureCreatedAsync();
        var store = scope.ServiceProvider.GetRequiredService<IEventStore>();
        // Separate writes establish known positions without relying on within-batch insertion order.
        for (var position = 1; position <= 5; position++)
            await store.WriteEvents([new EntryAdded(position % 2 == 1 ? "A" : "B", $"Event {position}")]);
    }

    static async Task ReverseUnorderedReads(IServiceScope scope)
    {
        var context = scope.ServiceProvider.GetRequiredService<EventStoreContext>();
        // Keep this reader's connection open: the PRAGMA is connection-local.
        await context.Database.OpenConnectionAsync();
        await context.Database.ExecuteSqlRawAsync("PRAGMA reverse_unordered_selects = ON;");
    }

    [SerializableEventPayload("ReadOrdering.EntryAdded")]
    public record EntryAdded(string JournalId, string Text)
        : EventPayload(new StreamId("ReadOrdering", JournalId), "ReadOrdering.EntryAdded");
}
