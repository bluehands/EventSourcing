# EventSourcing
Extensible, unobstrusive, functional and flexible multi persistence event sourcing framework for .NET. 

## Concepts & Usage

Install package bluehands.EventSourcing from [NuGet](https://www.nuget.org/packages/Bluehands.EventSourcing):

```
dotnet add package Bluehands.EventSourcing
```

Choose a persistence implementation that fits your needs. To start with the sqlite store is often a good choice.

```
dotnet add package Bluehands.EventSourcing.Persistence.EntityFramework.Sqlite
```

Add event sourcing services:
```csharp
services.AddEventSourcing(
  options => options.UseSqliteEventStore(@"Data Source=.\EventStore.db")
);
```

The following services will be available from your service provider:

 - **```IEventStore```** Offers methods to read and write events. Use ```IReadOnlyEventStore``` if write access is not required.
 - **```IObservable<Event>```** Event stream offering push based notifications when new events arrive.

### Lifecycle
To actually start listening to events on your event stream (and to allow for example persistence providers to initialize) use:

```csharp
...
IServiceProvider serviceProvider = builder.Build();
await serviceProvider.StartEventSourcing();
```

Your own services can implement IInializer<TPhase> to register initialize callbacks for certain lifecycle events.
```csharp
class SomethingToBeDoneBeforeEventReplay : IInializer<BeforeEventReplay>
{
  async Task Initialize() => ...
}

//register initializer:
services.AddInitializer<SomethingToBeDoneBeforeEventReplay>();
```

### Extensibility

Packages are designed to be easily extensible to support other persistence types, event serialization formats (like binary payload serialization) or new Lifecycle phases (see AfterEventReplayPhase introduces by Bluehands.EventSourcing.Funicular.Commands). Exensibility patterns are inspired by the ones used in current EntityFramework versions.

### Command layer

### Projections

