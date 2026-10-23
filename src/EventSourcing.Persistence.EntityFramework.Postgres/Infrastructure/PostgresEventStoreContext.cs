using Microsoft.EntityFrameworkCore;

namespace EventSourcing.Persistence.EntityFramework.Postgres.Infrastructure;

public class PostgresEventStoreContext(DbContextOptions<PostgresEventStoreContext> options) : EventStoreContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Event>(entity =>
        {
            entity.ToTable("Events", "public");
            entity.Property(e => e.Position).ValueGeneratedNever();
            entity.Property(e => e.StreamType).HasMaxLength(128);
            entity.Property(e => e.StreamId).HasMaxLength(256);
            entity.Property(e => e.EventType).HasMaxLength(450);
            entity.Property(e => e.Timestamp)
                .HasConversion(timestamp => timestamp.ToUniversalTime(), timestamp => timestamp)
                .HasColumnType("timestamp with time zone");
        });
        // PostgreSQL defaults to CACHE 1 and NO CYCLE. The append protocol requires both.
        modelBuilder.HasSequence<long>("EventPosition", "public");
    }
}
