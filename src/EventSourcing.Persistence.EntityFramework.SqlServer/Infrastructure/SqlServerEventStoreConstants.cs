namespace EventSourcing.Persistence.EntityFramework.SqlServer.Infrastructure;

internal static class SqlServerEventStoreConstants
{
    // nvarchar lengths are measured in UTF-16 code units, including surrogate pairs.
    // Changing these requires a schema migration; existing migrations retain their literal sizes.
    public const int StreamTypeMaxLength = 128;
    public const int StreamIdMaxLength = 256;
    public const int EventTypeMaxLength = 450;

    public const int ScalarBatchMaxCount = 10;
    public const int BulkEventThreshold = 1000;
    public const long BulkByteThreshold = 4 * 1024 * 1024;
    public const uint BrokerMaxRowsPerSelect = 10000;

    public const int Utf16BytesPerCodeUnit = 2;
    // InputOrdinal (bigint) and Timestamp (SqlClient datetimeoffset representation).
    public const int EstimatedFixedRowBytes = sizeof(long) + 16;
}
