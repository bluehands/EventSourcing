using System.Data.Common;
using System.Text.Json;
using EventSourcing.Commands;
using EventSourcing.Commands.SerializablePayloads;
using EventSourcing.Persistence.EntityFramework;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace EventSourcing.Test;

[TestClass]
[Ignore]
public class SqlServerBatchOrderingTest
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [TestCategory("SqlServerIntegration")]
    [DataRow(2)]
    [DataRow(4)]
    [DataRow(41)]
    [DataRow(42)]
    [DataRow(43)]
    [DataRow(50)]
    [DataRow(100)]
    public async Task PersistedPositionsPreserveInputOrderAndCompletionMarkerIsLast(int eventCount)
    {
        var connectionString = Environment.GetEnvironmentVariable("TEST_SQLSERVER_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
            Assert.Inconclusive("Set TEST_SQLSERVER_CONNECTION_STRING to run the live SQL Server batch-order experiment.");

        var connection = new SqlConnectionStringBuilder(connectionString)
        {
            InitialCatalog = $"EventSourcing_BatchOrder_{Guid.NewGuid():N}"
        };
        var capture = new CaptureWrites();
        await using var services = new ServiceCollection()
            .AddEventSourcing(options => options
                .UseSqlServerEventStore(connection.ConnectionString)
                .PayloadAssemblies(typeof(SqlServerBatchOrderingTest).Assembly))
            .AddSingleton<EventPayloadMapper>(new CommandProcessedMapper<string, FailedSaveIsolationTest.ErrorPayload>())
            .AddDbContext<EventStoreContext>(options => options.AddInterceptors(capture))
            .BuildServiceProvider();

        try
        {
            using (var scope = services.CreateScope())
                await scope.ServiceProvider.GetRequiredService<EventStoreContext>().Database.MigrateAsync();

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

                capture.Commands.Clear();
                long fromPosition;
                using (var scope = services.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<EventStoreContext>();
                    fromPosition = (await context.Events.MaxAsync(e => (long?)e.Position) ?? 0) + 1;
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

                // SQL parameter order checks the provider's input; the persisted read above checks identity allocation.
                capture.Commands.SelectMany(c => c.Sequences).Should().Equal(expected);
                capture.Commands.Should().HaveCount((eventCount + 41) / 42);
                capture.Commands.Should().OnlyContain(c => c.Sql.Contains("WITH (TABLOCKX)"));
                if (iteration == 0)
                    foreach (var command in capture.Commands)
                        TestContext.WriteLine($"Sequences: {string.Join(",", command.Sequences)}\n{command.Sql}");
            }

            TestContext.WriteLine($"25 batches of {eventCount} events preserved input order and completion-marker position.");
        }
        finally
        {
            using var scope = services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<EventStoreContext>().Database.EnsureDeletedAsync();
        }
    }

    [SerializableEventPayload("SqlServerBatchOrdering.OrderedEntry")]
    public record OrderedEntry(int Sequence, string JournalId, string Text)
        : EventPayload(new StreamId("BatchOrdering", JournalId), "SqlServerBatchOrdering.OrderedEntry");

    sealed record CapturedWrite(string Sql, int[] Sequences);

    sealed class CaptureWrites : DbCommandInterceptor
    {
        public List<CapturedWrite> Commands { get; } = [];

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("INSERT INTO [Events]") || command.CommandText.Contains("MERGE [Events]"))
            {
                var sequences = new List<int>();
                foreach (DbParameter parameter in command.Parameters)
                {
                    if (parameter.Value is not string json || !json.StartsWith('{'))
                        continue;
                    using var document = JsonDocument.Parse(json);
                    if (document.RootElement.TryGetProperty("Sequence", out var sequence))
                        sequences.Add(sequence.GetInt32());
                    else if (document.RootElement.TryGetProperty("CommandId", out _))
                        sequences.Add(-1);
                }
                Commands.Add(new(command.CommandText, sequences.ToArray()));
            }
            return new(result);
        }
    }
}
