using EventSourcing.Persistence.EntityFramework.Postgres.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace EventSourcing.Persistence.EntityFramework.Postgres;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<PostgresEventStoreContext>
{
    public PostgresEventStoreContext CreateDbContext(string[] args) => new(
        new DbContextOptionsBuilder<PostgresEventStoreContext>()
            .UseNpgsql("Host=localhost;Database=EventSourcingMigrations", postgres => postgres
                .MigrationsAssembly(typeof(PostgresEventStoreContext).Assembly.GetName().Name!)
                .MigrationsHistoryTable("__EFMigrationsHistory", "public"))
            .Options);
}
