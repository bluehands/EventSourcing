# EventSourcing tests

## PostgreSQL integration tests

From the repository root, supply a connection to a PostgreSQL server whose user can create/drop isolated test databases:

```powershell
$env:TEST_POSTGRES_CONNECTION_STRING = 'Host=localhost;Port=5432;Username=postgres;Password=your-password'
dotnet test --project src/EventSourcing.Test -- --filter-trait Category=PostgresIntegration
```

Tests create and remove their own databases. CI provisions PostgreSQL and sets `TEST_POSTGRES_REQUIRED=true` so a missing connection fails the run. Without the connection variable, optional local runs skip these tests.
