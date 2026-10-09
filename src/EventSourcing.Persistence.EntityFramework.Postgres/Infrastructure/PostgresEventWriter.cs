using System.Text;
using System.Transactions;
using EventSourcing.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace EventSourcing.Persistence.EntityFramework.Postgres.Infrastructure;

internal sealed class PostgresEventWriter(PostgresEventStoreContext context) : IEventWriter<Event>
{
    // Transfer policy, not a public batch limit. COPY avoids whole-batch column arrays for large transfers.
    const int CopyEventThreshold = 1024;
    const long CopyByteThreshold = 1024 * 1024;
    const string Columns = "\"Position\", \"StreamType\", \"StreamId\", \"EventType\", \"Payload\", \"Timestamp\"";

    // The sequence OID distinguishes stores in the same database. Materialized dependencies enforce
    // lock-before-nextval-before-setval. CACHE 1 is essential; rollback gaps are intentional.
    const string AllocateSql = """
        WITH locked AS MATERIALIZED (
            SELECT pg_advisory_xact_lock(19467218, 'public."EventPosition"'::regclass::oid::int)
        ), first AS MATERIALIZED (
            SELECT nextval('public."EventPosition"') AS position FROM locked
        )
        SELECT setval('public."EventPosition"', first.position + $1::bigint - 1, true) AS last_position FROM first
        """;
    const string ArraysSql = "WITH allocated AS (" + AllocateSql + ") INSERT INTO public.\"Events\" (" + Columns + ") " +
        "SELECT allocated.last_position - $1 + input.ordinal, input.st, input.sid, input.et, input.payload, input.ts " +
        "FROM allocated CROSS JOIN unnest($2::text[], $3::text[], $4::text[], $5::text[], $6::timestamptz[]) " +
        "WITH ORDINALITY AS input(st, sid, et, payload, ts, ordinal)";

    public async Task WriteEvents(IEnumerable<Event> payloads)
    {
        // Complete lazy serialization before taking any database lock.
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

        if (UseCopy(events))
            await Copy(events);
        else
            await InsertArrays(events);
    }

    static bool UseCopy(Event[] events)
    {
        if (events.Length >= CopyEventThreshold)
            return true;
        long bytes = 0;
        foreach (var e in events)
        {
            // Include UTF-8 text, position/timestamp and binary field framing, not just payload characters.
            bytes += 42L + Utf8Bytes(e.StreamType) + Utf8Bytes(e.StreamId) + Utf8Bytes(e.EventType) + Utf8Bytes(e.Payload);
            if (bytes >= CopyByteThreshold)
                return true;
        }
        return false;
    }

    static int Utf8Bytes(string? value) => value == null ? 0 : Encoding.UTF8.GetByteCount(value);

    async Task InsertArrays(Event[] events)
    {
        await using var command = new NpgsqlCommand(ArraysSql) { CommandTimeout = context.Database.GetCommandTimeout() ?? 30 };
        command.Parameters.Add(new NpgsqlParameter<long> { TypedValue = events.Length });
        AddArray(command, events.Select(e => e.StreamType).ToArray(), NpgsqlDbType.Text);
        AddArray(command, events.Select(e => e.StreamId).ToArray(), NpgsqlDbType.Text);
        AddArray(command, events.Select(e => e.EventType).ToArray(), NpgsqlDbType.Text);
        AddArray(command, events.Select(e => e.Payload).ToArray(), NpgsqlDbType.Text);
        AddArray(command, events.Select(e => e.Timestamp.UtcDateTime).ToArray(), NpgsqlDbType.TimestampTz);
        await context.Database.OpenConnectionAsync();
        try
        {
            command.Connection = (NpgsqlConnection)context.Database.GetDbConnection();
            // One statement owns an implicit transaction; its response follows commit. The advisory lock
            // is retained until then, so no lower position can become visible after a higher one.
            if (await command.ExecuteNonQueryAsync() != events.Length)
                throw new InvalidOperationException("PostgreSQL append count mismatch.");
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }

    static void AddArray<T>(NpgsqlCommand command, T[] values, NpgsqlDbType elementType) =>
        command.Parameters.Add(new NpgsqlParameter<T[]> { TypedValue = values, NpgsqlDbType = NpgsqlDbType.Array | elementType });

    async Task Copy(Event[] events)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
        var connection = (NpgsqlConnection)context.Database.GetDbConnection();
        var timeout = context.Database.GetCommandTimeout() ?? 30;
        await using var allocate = new NpgsqlCommand(AllocateSql, connection) { CommandTimeout = timeout };
        allocate.Parameters.Add(new NpgsqlParameter<long> { TypedValue = events.Length });
        var last = (long)(await allocate.ExecuteScalarAsync())!;
        var first = checked(last - events.Length + 1);
        await using (var importer = await connection.BeginBinaryImportAsync($"COPY public.\"Events\" ({Columns}) FROM STDIN (FORMAT BINARY)"))
        {
            importer.Timeout = TimeSpan.FromSeconds(timeout);
            for (var i = 0; i < events.Length; i++)
            {
                var e = events[i];
                await importer.StartRowAsync();
                await importer.WriteAsync(checked(first + i), NpgsqlDbType.Bigint);
                await WriteText(importer, e.StreamType);
                await WriteText(importer, e.StreamId);
                await WriteText(importer, e.EventType);
                await WriteText(importer, e.Payload);
                await importer.WriteAsync(e.Timestamp.UtcDateTime, NpgsqlDbType.TimestampTz);
            }
            if (await importer.CompleteAsync() != (ulong)events.Length)
                throw new InvalidOperationException("PostgreSQL COPY count mismatch.");
        }
        await transaction.CommitAsync();
    }

    static Task WriteText(NpgsqlBinaryImporter importer, string? value) =>
        value == null ? importer.WriteNullAsync() : importer.WriteAsync(value, NpgsqlDbType.Text);
}
