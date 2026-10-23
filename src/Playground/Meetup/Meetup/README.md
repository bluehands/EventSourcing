## Meetup example

This is a minimal example demonstrating how event sourcing packages with the optional EventSourcing.Commands layer can be used. We set up an app for planning user group talks. Talks can be added by a speaker with some meta information an a maximum number of attendees. Attendees can register for talks and are added to the attendees list or a waitlist, if number of maximum number of attendees is exeeded.

Our asp net core web application offers a graphql api with query, mutation and subscription endpoints. Multiple instances of the app can be run and every instance is notified via the event stream and updates its state accordingly.

### Discaimler
In the example app domain events are serialized to the event store directly (see `SerializableEventPayload` attribute on event records). This is recommended only for rapid prototyping / testing purposes only. If you move to production separate domain and persisted events and use an [EventPayloadMapper](https://github.com/bluehands/EventSourcing/blob/main/src/EventSourcing/EventPayloadMapper.cs) to map between the two representations. That way you are free to restructure / rename you domain event while keeping you serialized contracts compatible. 
Also, in a real world application one would use different projections at the api and for validation at the command layer. 

### Try it

Clone [Bluehands.EventSourcing](https://github.com/bluehands/EventSourcing).

To start the sample app navigate to src/Playground/Meetup/Meetup project folder and type:
```powershell
dotnet run --urls http://localhost:60868
```
Optionally, you can start another instance:
```powershell
dotnet run --urls http://localhost:60869
```

### Run with Aspire and PostgreSQL

With the .NET 10 SDK and a running Docker-compatible runtime using Linux containers,
run this command from the repository root:

```powershell
dotnet run --project src/Playground/Meetup/Meetup.AppHost
```

No Aspire workload or CLI installation is required. The AppHost starts PostgreSQL,
creates the `events` database, and passes its connection string to this existing
Meetup application as `ConnectionStrings:Postgres`. Do not also configure a SQL
Server connection string in the Meetup application's environment or configuration.

Open the dashboard URL printed by the AppHost. Once `meetup` has started, open
its HTTP endpoint from the dashboard, then navigate to `/graphql` and try the
operations below. Aspire assigns a free port rather than using the standalone
instance's port 60868. Meetup applies event-store migrations and waits for projection replay
before accepting requests. The AppHost uses HTTP without the standalone project's
HTTPS launch profile; no development certificate is needed.

PostgreSQL uses a Docker volume, so talks and registrations survive stopping and
restarting the AppHost. Create a talk, restart the AppHost, then query `talks` to
verify that the projection is rebuilt from persisted events. Removing the volume
deletes that history. Existing SQLite or other PostgreSQL databases are not imported.

This setup is for local development: the API has no authentication and the API
and dashboard use HTTP on localhost. Do not expose them publicly. No custom health
checks or OpenTelemetry instrumentation are added.

### Use a local PostgreSQL database

SQLite (`EventStore.db`) remains the default. To use PostgreSQL, start a local PostgreSQL server and set the connection string before running the app from this project folder:

```powershell
$env:ConnectionStrings__Postgres = 'Host=localhost;Port=5432;Database=meetup;Username=postgres;Password=your-password'
dotnet run --no-launch-profile --urls http://localhost:60868
```

Replace the username and password with your local credentials. Keep passwords out of committed configuration files. The app applies the PostgreSQL provider migrations at startup; the database user needs permission to create the `meetup` database if it does not exist, and to create its tables and sequence. Use a dedicated sample database, not an existing application database. SQLite events are not copied to PostgreSQL.

For a second instance, set the same connection string in another terminal and run on port 60869. Both instances share the database and receive new events through polling. With neither server connection configured, the sample selects SQLite again:

```powershell
Remove-Item Env:ConnectionStrings__Postgres -ErrorAction SilentlyContinue
Remove-Item Env:ConnectionStrings__SqlServer -ErrorAction SilentlyContinue
```

### Use a local SQL Server database

For SQL Server Express LocalDB, set the connection string before running the app from this project folder:

```powershell
Remove-Item Env:ConnectionStrings__Postgres -ErrorAction SilentlyContinue
$env:ConnectionStrings__SqlServer = 'Server=(localdb)\MSSQLLocalDB;Database=Meetup;Integrated Security=True;TrustServerCertificate=True'
dotnet run --no-launch-profile --urls http://localhost:60868
```

For another local instance, replace `Server=(localdb)\MSSQLLocalDB` with your instance name, such as `Server=.\SQLEXPRESS`. The app creates the database if permitted and applies migrations at startup. Use a dedicated sample database and an account allowed to create its schema. `TrustServerCertificate=True` is for local development, not a production certificate policy.

Multiple instances can share the same SQL Server connection string; new events arrive through polling, without Service Broker setup. Events are not migrated between providers. Configure only one of `ConnectionStrings__Postgres` and `ConnectionStrings__SqlServer`; setting both is a startup error, including when one comes from an appsettings file.

The sample references the event-sourcing projects in this repository, including both server providers, so no separately published provider packages are required.

### GraphQL API

Open http://localhost:60868 for sample operations and a link to the GraphQL playground at http://localhost:60868/graphql. Try these operations in the playground:
```gql
query getTalks {
  talks {
    id
    title
    attendees {
      name
      isOnWaitList
    }
    talkPublished
    maxAttendees
    bookedUpWithin
  }
}

mutation addTalk {
  newUserGroupTalk(title: "AppIn20Minutes", maxAttendees: 5) {
    id
    title    
  }
}

mutation register {
  registerParticipant(talkId: "<your talk id>", name: "Alex", mailAddress: "abc@bluehands.de") 
}
```
You might want to open a second brower tab, navigate to your second instance on http://localhost:60869/graphql and listen for changes over a web socket subscription:
```gql
subscription {
  onTalkChanged {
    changeInfo
    talk {
      id    
      bookedUpWithin
      maxAttendees
      talkPublished
      title
      attendees {
        name
        isOnWaitList
      }
    }
  }
}
```
