using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace EventSourcing.Persistence.EntityFramework.Sqlite;

internal class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<EventStoreContext>
{
    public EventStoreContext CreateDbContext(string[] args)
    {
        var connectionString = $"DataSource=MigrationsDb.db";
        var options = new DbContextOptionsBuilder<EventStoreContext>()
            .UseSqlite(connectionString, sqlite => sqlite.MigrationsAssembly(typeof(DesignTimeDbContextFactory).Assembly.FullName))
            .Options;
        return new EventStoreContext(options);
    }
}