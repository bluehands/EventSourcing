using EventSourcing.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace EventSourcing.Persistence.EntityFramework.Infrastructure;

public class DatabaseInitializer(EventStoreContext eventStoreContext) : IInitializer<SchemaInitialization>
{
    public virtual async Task Initialize()
    {
        await eventStoreContext.Database.MigrateAsync();
    }
}