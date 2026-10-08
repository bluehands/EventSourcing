using System.Data.Common;
using System.Text.RegularExpressions;
using EventSourcing.Persistence.EntityFramework;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using StoredEvent = EventSourcing.Persistence.EntityFramework.Event;

namespace EventSourcing.Test;

[TestClass]
public class SqlServerExclusiveWriteTest
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(1, false)]
    [DataRow(2, false)]
    [DataRow(4, false)]
    [DataRow(50, false)]
    [DataRow(1, true)]
    [DataRow(2, true)]
    [DataRow(4, true)]
    [DataRow(50, true)]
    public async Task FirstGeneratedWriteCommandLocksEveryEventWrite(int eventCount, bool saveAsync)
    {
        var capture = new CaptureWriteCommand();
        await using var services = new ServiceCollection()
            .AddEventSourcing(options => options.UseSqlServerEventStore(
                "Server=unused;Database=EventWriteRegression;Integrated Security=true;TrustServerCertificate=true"))
            // Added after the production registration so capture observes the rewritten SQL.
            .AddDbContext<EventStoreContext>(options => options.AddInterceptors(new SuppressConnectionOpen(), capture))
            .BuildServiceProvider();
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EventStoreContext>();
        // No database is contacted. Stop at the first command, before execution; avoid transaction creation.
        context.Database.AutoTransactionBehavior = AutoTransactionBehavior.Never;
        for (var i = 0; i < eventCount; i++)
            context.Events.Add(new StoredEvent(0, "LockRegression", "A", "EntryAdded", $"{{\"index\":{i}}}", DateTimeOffset.UtcNow));

        try
        {
            if (saveAsync)
                await context.SaveChangesAsync();
            else
                context.SaveChanges();
            Assert.Fail("Expected SQL capture to stop command execution.");
        }
        catch (DbUpdateException exception) when (exception.InnerException is CommandCapturedException)
        {
        }

        capture.CommandText.Should().NotBeNullOrEmpty();
        TestContext.WriteLine(capture.CommandText!);
        var writes = Regex.Matches(capture.CommandText!, @"(?:INSERT INTO|MERGE) \[Events\](?: WITH \([^)]*\))?", RegexOptions.IgnoreCase);
        writes.Should().NotBeEmpty("the real provider must generate an event-table write");
        foreach (Match write in writes)
            write.Value.Should().Contain("WITH (TABLOCKX)", "every event-table write must acquire writer serialization before allocating positions");
    }

    sealed class CommandCapturedException : Exception;

    sealed class CaptureWriteCommand : DbCommandInterceptor
    {
        public string? CommandText { get; private set; }

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
        {
            CommandText = command.CommandText;
            throw new CommandCapturedException();
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            CommandText = command.CommandText;
            throw new CommandCapturedException();
        }
    }

    sealed class SuppressConnectionOpen : DbConnectionInterceptor
    {
        public override InterceptionResult ConnectionOpening(
            DbConnection connection, ConnectionEventData eventData, InterceptionResult result) => InterceptionResult.Suppress();

        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(
            DbConnection connection, ConnectionEventData eventData, InterceptionResult result,
            CancellationToken cancellationToken = default) => new(InterceptionResult.Suppress());
    }
}
