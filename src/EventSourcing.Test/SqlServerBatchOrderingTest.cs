using EventSourcing.Commands;
using EventSourcing.Commands.SerializablePayloads;
using EventSourcing.Persistence.EntityFramework;
using AwesomeAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EventSourcing.Test;

public class SqlServerBatchOrderingTest(ITestOutputHelper output)
{
    [Theory]
    [Trait("Category", "SqlServerIntegration")]
    [InlineData(2)]
    [InlineData(10)]
    [InlineData(43)]
    [InlineData(100)]
    [InlineData(1000)]
    public async Task PersistedPositionsPreserveInputOrderAndCompletionMarkerIsLast(int eventCount)
    {
        var connectionString = Environment.GetEnvironmentVariable("TEST_SQLSERVER_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw Xunit.Sdk.SkipException.ForSkip(
                "Set TEST_SQLSERVER_CONNECTION_STRING to run SQL Server integration tests.");

        var connection = new SqlConnectionStringBuilder(connectionString)
        {
            InitialCatalog = $"EventSourcing_BatchOrder_{Guid.NewGuid():N}"
        };
        await using var services = new ServiceCollection()
            .AddEventSourcing(options => options
                .UseSqlServerEventStore(connection.ConnectionString)
                .PayloadAssemblies(typeof(SqlServerBatchOrderingTest).Assembly))
            .AddSingleton<EventPayloadMapper>(new CommandProcessedMapper<string, FailedSaveIsolationTest.ErrorPayload>())
            .BuildServiceProvider();

        try
        {
            using (var scope = services.CreateScope())
                await scope.ServiceProvider.GetRequiredService<EventStoreContext>().Database.MigrateAsync(TestContext.Current.CancellationToken);

            for (var iteration = 0; iteration < 25; iteration++)
            {
                // Vary stream keys and row widths without making sequence recoverable by lexical sorting.
                var textLength = (iteration % 3) switch { 0 => 1, 1 => 128, _ => 4096 };
                var commandId = CommandId.NewCommandId();
                IReadOnlyCollection<IEventPayload> payloads =
                [
                    .. Enumerable.Range(0, eventCount - 1).Select(sequence =>
                        new OrderedEntry(sequence, $"Stream-{(sequence * 17 + iteration) % 7}",
                            new string((char)('a' + sequence % 26), textLength + sequence % 5))),
                    new CommandProcessed<string>(CommandResult<string>.Processed(commandId, FunctionalResult<string>.Ok("Done")))
                ];

                long fromPosition;
                using (var scope = services.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<EventStoreContext>();
                    fromPosition = (await context.Events.MaxAsync(e => (long?)e.Position, TestContext.Current.CancellationToken) ?? 0) + 1;
                    await scope.ServiceProvider.GetRequiredService<IEventStore>().WriteEvents(payloads);
                }

                // Read through the real event-store mapper and ordered reader in a fresh scope.
                var events = new List<Event>();
                using (var scope = services.CreateScope())
                    await foreach (var @event in scope.ServiceProvider.GetRequiredService<IEventStore>().ReadEvents(fromPosition))
                        events.Add(@event);

                var expected = Enumerable.Range(0, eventCount - 1).Append(-1).ToArray();
                events.Select(e => e.Payload switch
                    {
                        OrderedEntry entry => entry.Sequence,
                        CommandProcessed<string> => -1,
                        _ => throw new InvalidOperationException($"Unexpected payload: {e.Payload}")
                    })
                    .Should().Equal(expected, $"persisted input order must survive batch size {eventCount}, iteration {iteration}");
                events[^1].Payload.Should().BeOfType<CommandProcessed<string>>()
                    .Which.CommandId.Should().Be(commandId);
                events.Select(e => e.Position).Should().BeInAscendingOrder().And.OnlyHaveUniqueItems();

            }

            output.WriteLine($"25 batches of {eventCount} events preserved input order and completion-marker position.");
        }
        finally
        {
            using var scope = services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<EventStoreContext>().Database.EnsureDeletedAsync(TestContext.Current.CancellationToken);
        }
    }

    [SerializableEventPayload("SqlServerBatchOrdering.OrderedEntry")]
    public record OrderedEntry(int Sequence, string JournalId, string Text)
        : EventPayload(new StreamId("BatchOrdering", JournalId), "SqlServerBatchOrdering.OrderedEntry");

}
