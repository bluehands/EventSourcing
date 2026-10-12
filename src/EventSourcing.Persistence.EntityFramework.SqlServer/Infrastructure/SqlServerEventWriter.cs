using System.Collections;
using System.Data;
using System.Data.Common;
using EventSourcing.Infrastructure;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClient.Server;
using Microsoft.EntityFrameworkCore;

namespace EventSourcing.Persistence.EntityFramework.SqlServer.Infrastructure;

// Identity positions are allocated only while holding an exclusive Events table lock.
// The lock remains held through durable commit, so pollers cannot miss late lower positions.
public sealed class SqlServerEventWriter(EventStoreContext context,
    int bulkEventThreshold = SqlServerEventStoreConstants.BulkEventThreshold,
    long bulkByteThreshold = SqlServerEventStoreConstants.BulkByteThreshold) : IEventWriter<Event>
{
    const string InsertEvents = """
        INSERT INTO dbo.Events WITH (TABLOCKX) (StreamType, StreamId, EventType, Payload, Timestamp)
        SELECT StreamType, StreamId, EventType, Payload, Timestamp
        FROM @events ORDER BY InputOrdinal;
        """;

    public async Task WriteEvents(IEnumerable<Event> payloads)
    {
        // Mapping/serialization is lazy upstream; finish it before opening a transaction or locking.
        var events = payloads.ToArray();
        if (events.Length == 0) return;
        long bytes = 0;
        foreach (var e in events)
        {
            ValidateIdentifier(e.StreamType, SqlServerEventStoreConstants.StreamTypeMaxLength, nameof(e.StreamType));
            ValidateIdentifier(e.StreamId, SqlServerEventStoreConstants.StreamIdMaxLength, nameof(e.StreamId));
            ValidateIdentifier(e.EventType, SqlServerEventStoreConstants.EventTypeMaxLength, nameof(e.EventType));
            ArgumentNullException.ThrowIfNull(e.Payload);
            bytes += SqlServerEventStoreConstants.Utf16BytesPerCodeUnit *
                (e.StreamType.Length + e.StreamId.Length + e.EventType.Length + (long)e.Payload.Length) +
                SqlServerEventStoreConstants.EstimatedFixedRowBytes;
        }

        // A dedicated connection/transaction keeps failed appends out of the EF change tracker
        // and prevents an ambient transaction from acknowledging an uncommitted append.
        var connectionString = new SqlConnectionStringBuilder(context.Database.GetConnectionString()) { Enlist = false };
        await using var connection = new SqlConnection(connectionString.ConnectionString);
        await connection.OpenAsync();
        var timeout = context.Database.GetCommandTimeout() ?? connection.CommandTimeout;
        await using var command = connection.CreateCommand();
        command.CommandTimeout = timeout;

        if (events.Length < bulkEventThreshold && bytes < bulkByteThreshold)
        {
            // A single INSERT is an atomic autocommit transaction. TABLOCKX lasts through commit.
            var insert = InsertEvents;
            // Avoid structured-parameter setup for small appends.
            // int.MaxValue selects TVP-only writes for the benchmark runner.
            if (events.Length <= SqlServerEventStoreConstants.ScalarBatchMaxCount && bulkEventThreshold != int.MaxValue)
            {
                var values = new string[events.Length];
                for (var i = 0; i < events.Length; i++)
                {
                    var e = events[i];
                    values[i] = $"({i}, @type{i}, @id{i}, @event{i}, @payload{i}, @timestamp{i})";
                    command.Parameters.Add($"@type{i}", SqlDbType.NVarChar, SqlServerEventStoreConstants.StreamTypeMaxLength).Value = e.StreamType;
                    command.Parameters.Add($"@id{i}", SqlDbType.NVarChar, SqlServerEventStoreConstants.StreamIdMaxLength).Value = e.StreamId;
                    command.Parameters.Add($"@event{i}", SqlDbType.NVarChar, SqlServerEventStoreConstants.EventTypeMaxLength).Value = e.EventType;
                    command.Parameters.Add($"@payload{i}", SqlDbType.NVarChar, -1).Value = e.Payload;
                    command.Parameters.Add($"@timestamp{i}", SqlDbType.DateTimeOffset).Value = e.Timestamp;
                }
                insert = $"""
                    INSERT INTO dbo.Events WITH (TABLOCKX) (StreamType, StreamId, EventType, Payload, Timestamp)
                    SELECT StreamType, StreamId, EventType, Payload, Timestamp
                    FROM (VALUES {string.Join(",", values)}) AS input(InputOrdinal, StreamType, StreamId, EventType, Payload, Timestamp)
                    ORDER BY InputOrdinal;
                    """;
            }
            else
                command.Parameters.Add(new SqlParameter("@events", SqlDbType.Structured)
                {
                    TypeName = "dbo.EventBatch",
                    Value = Records(events)
                });
            // ORDER BY guarantees identity computation order, not physical row insertion order.
            command.CommandText = "SET XACT_ABORT ON; " + insert;
            await command.ExecuteNonQueryAsync();
        }
        else
        {
            await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted);
            command.Transaction = transaction;
            // Parameterized DDL runs in sp_executesql's nested scope, which would drop
            // the local temporary table on return. Create it in the session batch scope.
            // Transfer before taking the publication lock. Only the final ordered insertion
            // allocates identities and fires event-table triggers; staging is connection-local.
            command.CommandText = $"""
                SET XACT_ABORT ON;
                CREATE TABLE #EventBatch (
                    InputOrdinal bigint NOT NULL PRIMARY KEY,
                    StreamType nvarchar({SqlServerEventStoreConstants.StreamTypeMaxLength}) NOT NULL,
                    StreamId nvarchar({SqlServerEventStoreConstants.StreamIdMaxLength}) NOT NULL,
                    EventType nvarchar({SqlServerEventStoreConstants.EventTypeMaxLength}) NOT NULL,
                    Payload nvarchar(max) NOT NULL,
                    Timestamp datetimeoffset NOT NULL
                );
                """;
            await command.ExecuteNonQueryAsync();
            await using var reader = new EventDataReader(events);
            using var bulk = new SqlBulkCopy(connection, SqlBulkCopyOptions.Default, transaction);
            bulk.DestinationTableName = "#EventBatch";
            bulk.EnableStreaming = true;
            bulk.BulkCopyTimeout = timeout;
            for (var i = 0; i < reader.FieldCount; i++)
                bulk.ColumnMappings.Add(i, reader.GetName(i));
            await bulk.WriteToServerAsync(reader);
            command.CommandText = InsertEvents.Replace("@events", "#EventBatch");
            await command.ExecuteNonQueryAsync();
            await transaction.CommitAsync();
        }
    }

    static void ValidateIdentifier(string value, int limit, string name)
    {
        ArgumentNullException.ThrowIfNull(value, name);
        // SQL Server nvarchar limits are UTF-16 code units (including surrogate pairs).
        if (value.Length > limit)
            throw new ArgumentException($"{name} must not exceed {limit} UTF-16 code units.", name);
    }

    static IEnumerable<SqlDataRecord> Records(Event[] events)
    {
        var record = new SqlDataRecord(
            new SqlMetaData("InputOrdinal", SqlDbType.BigInt),
            new SqlMetaData("StreamType", SqlDbType.NVarChar, SqlServerEventStoreConstants.StreamTypeMaxLength),
            new SqlMetaData("StreamId", SqlDbType.NVarChar, SqlServerEventStoreConstants.StreamIdMaxLength),
            new SqlMetaData("EventType", SqlDbType.NVarChar, SqlServerEventStoreConstants.EventTypeMaxLength),
            new SqlMetaData("Payload", SqlDbType.NVarChar, SqlMetaData.Max),
            new SqlMetaData("Timestamp", SqlDbType.DateTimeOffset));
        for (var i = 0; i < events.Length; i++)
        {
            var e = events[i];
            record.SetInt64(0, i);
            record.SetString(1, e.StreamType);
            record.SetString(2, e.StreamId);
            record.SetString(3, e.EventType);
            record.SetString(4, e.Payload);
            record.SetDateTimeOffset(5, e.Timestamp);
            yield return record;
        }
    }

    // Streams existing serialized rows directly; no DataTable or second payload buffer.
    sealed class EventDataReader(Event[] events) : DbDataReader
    {
        static readonly string[] Names = ["InputOrdinal", "StreamType", "StreamId", "EventType", "Payload", "Timestamp"];
        int _index = -1;
        bool _closed;
        public override bool Read() => ++_index < events.Length;
        public override Task<bool> ReadAsync(CancellationToken cancellationToken) => Task.FromResult(Read());
        public override int FieldCount => Names.Length;
        public override bool HasRows => events.Length > 0;
        public override bool IsClosed => _closed;
        public override int RecordsAffected => -1;
        public override int Depth => 0;
        public override object this[int ordinal] => GetValue(ordinal);
        public override object this[string name] => GetValue(GetOrdinal(name));
        public override string GetName(int ordinal) => Names[ordinal];
        public override int GetOrdinal(string name) => Array.IndexOf(Names, name);
        public override Type GetFieldType(int ordinal) => ordinal switch { 0 => typeof(long), 5 => typeof(DateTimeOffset), _ => typeof(string) };
        public override string GetDataTypeName(int ordinal) => ordinal switch { 0 => "bigint", 5 => "datetimeoffset", _ => "nvarchar" };
        public override object GetValue(int ordinal) => ordinal switch
        {
            0 => (long)_index,
            1 => events[_index].StreamType,
            2 => events[_index].StreamId,
            3 => events[_index].EventType,
            4 => events[_index].Payload,
            5 => events[_index].Timestamp,
            _ => throw new IndexOutOfRangeException()
        };
        public override int GetValues(object[] values)
        {
            var count = Math.Min(values.Length, FieldCount);
            for (var i = 0; i < count; i++) values[i] = GetValue(i);
            return count;
        }
        public override bool IsDBNull(int ordinal) => false;
        public override long GetInt64(int ordinal) => (long)GetValue(ordinal);
        public override string GetString(int ordinal) => (string)GetValue(ordinal);
        public override T GetFieldValue<T>(int ordinal) => (T)GetValue(ordinal);
        public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length)
        {
            var text = GetString(ordinal);
            if (buffer is null) return text.Length;
            var count = Math.Min(length, text.Length - checked((int)dataOffset));
            text.CopyTo((int)dataOffset, buffer, bufferOffset, count);
            return count;
        }
        public override bool NextResult() => false;
        public override void Close() => _closed = true;
        public override IEnumerator GetEnumerator() => new DbEnumerator(this);
        public override bool GetBoolean(int ordinal) => (bool)GetValue(ordinal);
        public override byte GetByte(int ordinal) => (byte)GetValue(ordinal);
        public override char GetChar(int ordinal) => (char)GetValue(ordinal);
        public override DateTime GetDateTime(int ordinal) => (DateTime)GetValue(ordinal);
        public override decimal GetDecimal(int ordinal) => (decimal)GetValue(ordinal);
        public override double GetDouble(int ordinal) => (double)GetValue(ordinal);
        public override float GetFloat(int ordinal) => (float)GetValue(ordinal);
        public override Guid GetGuid(int ordinal) => (Guid)GetValue(ordinal);
        public override short GetInt16(int ordinal) => (short)GetValue(ordinal);
        public override int GetInt32(int ordinal) => (int)GetValue(ordinal);
        public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) => throw new NotSupportedException();
    }
}
