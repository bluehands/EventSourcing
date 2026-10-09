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
            entity.Property(e => e.Timestamp)
                .HasConversion(timestamp => timestamp.ToUniversalTime(), timestamp => timestamp)
                .HasColumnType("timestamp with time zone");
        });
        modelBuilder.Entity<EventPositionCounter>(entity =>
        {
            entity.ToTable("EventPositionCounter", "public", table =>
                table.HasCheckConstraint("CK_EventPositionCounter_SingleRow", "\"Id\" = 1 AND \"LastPosition\" >= 0"));
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.HasData(new EventPositionCounter { Id = 1, LastPosition = 0 });
        });
    }
}

internal sealed class EventPositionCounter
{
    public int Id { get; set; }
    public long LastPosition { get; set; }
}
