using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace EventSourcing.Persistence.EntityFramework;

public class EventStoreContext : DbContext
{
    public EventStoreContext(DbContextOptions<EventStoreContext> contextOptions) : base(contextOptions) { }

    protected EventStoreContext(DbContextOptions contextOptions) : base(contextOptions) { }

    public DbSet<Event> Events { get; set; } = null!;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.ConfigureWarnings(c =>
        {
            c.Log((RelationalEventId.CommandExecuting, LogLevel.Debug));
            c.Log((RelationalEventId.CommandExecuted, LogLevel.Debug));
        });
        optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
    }
}
