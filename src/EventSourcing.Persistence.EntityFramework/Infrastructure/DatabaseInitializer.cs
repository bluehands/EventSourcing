using EventSourcing.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EventSourcing.Persistence.EntityFramework.Infrastructure;

public class DatabaseInitializer(EventStoreContext eventStoreContext, ILogger<DatabaseInitializer>? logger = null) : IInitializer<SchemaInitialization>
{
    public virtual async Task Initialize()
    {
        try
        {
            if (logger?.IsEnabled(LogLevel.Debug) ?? false)
                logger?.LogDebug("Assert database is up to date. Schema script: {DatabaseCreateScript}", eventStoreContext.Database.GenerateCreateScript());
            await eventStoreContext.Database.MigrateAsync();
        }
        catch (Exception e)
        {
            logger?.LogError(e, "Database migration failed. Try to run script manually: {DatabaseCreateScript}", eventStoreContext.Database.GenerateCreateScript());
            throw;
        }
    }
}