using System.Data;
using System.Transactions;
using EventSourcing.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace EventSourcing.Persistence.EntityFramework.Postgres.Infrastructure;

internal sealed class PostgresEventWriter(PostgresEventStoreContext context) : IEventWriter<Event>
{
    public async Task WriteEvents(IEnumerable<Event> payloads)
    {
        // Complete lazy mapping/serialization before opening the transaction or taking the counter lock.
        var events = payloads.ToArray();
        if (events.Length == 0)
            return;
        if (context.Database.CurrentTransaction != null || Transaction.Current != null ||
            context.Database.GetEnlistedTransaction() != null)
            throw new InvalidOperationException("PostgreSQL event appends require a provider-owned transaction.");
        if (events.Any(e => e.Position != 0))
            throw new InvalidOperationException("New PostgreSQL events must have Position = 0.");
        if (context.ChangeTracker.HasChanges())
            throw new InvalidOperationException("The event persistence context has unrelated pending changes.");

        await using var transaction = await context.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
        Event[] positionedEvents = [];
        try
        {
            await using var command = context.Database.GetDbConnection().CreateCommand();
            command.Transaction = transaction.GetDbTransaction();
            command.CommandTimeout = context.Database.GetCommandTimeout() ?? 30;
            command.CommandText = """
                UPDATE public."EventPositionCounter"
                SET "LastPosition" = "LastPosition" + @count
                WHERE "Id" = 1
                RETURNING "LastPosition";
                """;
            var count = command.CreateParameter();
            count.ParameterName = "count";
            count.DbType = DbType.Int64;
            count.Value = (long)events.Length;
            command.Parameters.Add(count);
            var result = await command.ExecuteScalarAsync();
            if (result is not long lastPosition)
                throw new InvalidOperationException("The PostgreSQL event position counter is missing. Apply the provider migrations.");

            var firstPosition = checked(lastPosition - events.Length + 1);
            positionedEvents = events.Select((e, index) => e with
            {
                Position = checked(firstPosition + index),
                Timestamp = e.Timestamp.ToUniversalTime()
            }).ToArray();
            context.Events.AddRange(positionedEvents);
            await context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        finally
        {
            // Includes Unchanged entries after a successful save followed by a failed commit.
            // Transaction disposal rolls back any uncommitted allocation and inserts.
            foreach (var e in positionedEvents)
                context.Entry(e).State = EntityState.Detached;
        }
    }
}
