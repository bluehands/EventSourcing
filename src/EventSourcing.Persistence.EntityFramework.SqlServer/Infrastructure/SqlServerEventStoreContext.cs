using Microsoft.EntityFrameworkCore;

namespace EventSourcing.Persistence.EntityFramework.SqlServer.Infrastructure;

public class SqlServerEventStoreContext(DbContextOptions<SqlServerEventStoreContext> options) : EventStoreContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        var events = modelBuilder.Entity<Event>();
        events.ToTable("Events", "dbo");
        events.Property(e => e.Position).UseIdentityColumn();
        events.Property(e => e.StreamType).HasMaxLength(SqlServerEventStoreConstants.StreamTypeMaxLength);
        events.Property(e => e.StreamId).HasMaxLength(SqlServerEventStoreConstants.StreamIdMaxLength);
        events.Property(e => e.EventType).HasMaxLength(SqlServerEventStoreConstants.EventTypeMaxLength);
    }
}
