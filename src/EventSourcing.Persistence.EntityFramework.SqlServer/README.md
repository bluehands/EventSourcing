# SQL Server persistence

SQL Server uses polling for its event stream by default. Broker notifications remain an explicit opt-in via
`UseSqlServerEventStore(connectionString, sql => sql.UseBrokerNotificationEventStream())`.

## Append implementation

Native SQL Server appends use `TABLOCKX` held through commit,
identity positions allocated by explicitly ordered native inserts, scalar parameters for batches up to 10,
TVPs for medium batches and streaming bulk copy into temporary staging for large transfers.
Identifier limits are 128/256/450 UTF-16 code units for stream type/stream ID/event type; Unicode payloads remain unrestricted.

## Database administration

The unpublished initial schema was updated directly; recreate development databases created with the earlier schema.
Appends require INSERT on `dbo.Events` and REFERENCES/EXECUTE on `dbo.EventBatch`;
reads require SELECT on events. Use fully durable database configuration (`DELAYED_DURABILITY = DISABLED`, the SQL Server default).
Use `IEventStore` for appends: writes without the table-lock protocol or identity resets bypass safe publication.
