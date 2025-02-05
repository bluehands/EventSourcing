<img src="EventSourceror.jpg" width="58" style="float:left">

# EventSourcing
Extensible, unobstrusive, functional and flexible multi persistence event sourcing framework for .NET. Unobstrusive means that you are free to model your domain objects, aggregates or whatever the way you like it based on an event stream that is simply abstracted as `IObservable<Event>`.

Currently packages are available in prerelease versions only. Nevertheless they are the essence of event sourcing implementations running in production at different scale for several years at various persistence types, so feel encouraged to have a look :). 

## Basic Usage

Install package Bluehands.EventSourcing from [NuGet](https://www.nuget.org/packages/Bluehands.EventSourcing):

```
dotnet add package Bluehands.EventSourcing
```

Choose a persistence implementation that fits your needs. To start with the sqlite store is often a good choice.

```
dotnet add package Bluehands.EventSourcing.Persistence.Sqlite
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
 
 To configure further options on your event store, like the position to start from, use:
 
 ```csharp
services.AddEventSourcing(options => options
    .UseSqliteEventStore(@"Data Source=.\EventStore.db",
        storeOptions => storeOptions.UsePollingEventStream(            
            getPositionToStartFrom: () => Task.FromResult(142L) //get your starting position
        )
    )
);
 ``` 

## Lifecycle
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

## Example
A little meetup planner example is implemented [here](https://github.com/bluehands/EventSourcing/blob/main/src/Playground/Meetup/Meetup)

## Extensibility

Packages are designed to be easily extensible to support other persistence types, event serialization formats (like binary payload serialization) or new lifecycle phases (see ```AfterEventReplayPhase``` introduced by Bluehands.EventSourcing.Commands). Exensibility patterns are inspired by the ones used in current EntityFramework versions.

## Command layer

Your are basically free to model a command layer (or not) on top of your event sourcing infrastructure. [Bluehands.EventSourcing.Commands](https://www.nuget.org/packages/Bluehands.EventSourcing.Commands) package is a proposal of such a command layer offering the following features:

 - 'Functional approach': A command processor / handler is just a function that produces events given an intention (command).
 - Your command handlers can return a an error in case of functional failures. This error information is serialized to the event store and gives you a generic way to inform your caller about failed commands, without having to model everything into your application state. You can choose your error type freely. A source generator shipped with the package adds supporting types for your specific error type (TODO: sample links). 
 - Use a [Result](https://github.com/bluehands/Funicular-Switch) type that correspongs to you error type to ease validation and error handling in your command processors. See [RegisterParticipantCommandProcessor](https://github.com/bluehands/EventSourcing/blob/b285feedd0a18fec91dfb8381e169229e7b1bc57/src/Playground/Meetup/Meetup/Commands.cs#L21) for an example.
 - Possibility for the issuer of a command to wait until the effect of his command (the events) where processed by a particular projection. This is especially useful for scenarios were a non CQS[^1] api should be kept stable while evolving the underlying infrastructure towards event sourcing. Have a look a the [Meetup app api](https://github.com/bluehands/EventSourcing/blob/b285feedd0a18fec91dfb8381e169229e7b1bc57/src/Playground/Meetup/Meetup/Api.cs#L13) for an example of the ```SendCommandAndWaitUntilApplied``` method.

[^1]: An API that does not respect the '[Command Query Separation](https://de.wikipedia.org/wiki/Command-Query-Separation)' principle. So commands actually return data, i.e. UpdateSomething returns the updated entity.


## Projections

As for the command layer you are basically free to build your projections the way you want. Just use the event stream to subscribe to events and apply them to your state objects.

*Bluehands.EventSourcing.Projections* package will come soon. Look at the [Talks type](https://github.com/bluehands/EventSourcing/blob/main/src/Playground/Meetup/Meetup/Projection.cs) to see an example of an in memory projection that uses an immutable state representation. State changes can be observed in a [reactive way](https://github.com/bluehands/EventSourcing/blob/b285feedd0a18fec91dfb8381e169229e7b1bc57/src/Playground/Meetup/Meetup/Api.cs#L34).

## Error handling

When using event sourcing it is crucial to understand 'whatever happened in the past'. Events will be in your store just like you wrote them with Version 1 of your application. To keep your domain event flexible and your persisted events compatible you should use separate types for both of them and map using the [EventPayloadMapper](https://github.com/bluehands/EventSourcing/blob/main/src/EventSourcing/EventPayloadMapper.cs) mechanism. But things go wrong. Basically there are two types of read failures, permanent failures due do events that cannot be deserialized anymore and temporary failures that might occur if for example the database is unavailable.
Currently temporary failures (exceptions that occur when reading from persistence) are retried forever to guarantee a stable event stream. A policy will be injectable here in future version.
Default behavior for deserialization failures is that currupted events are skipped. An ```ICorruptedEventHandler``` can be registered to adapt this behavior (look at this [test case](https://github.com/bluehands/EventSourcing/blob/b285feedd0a18fec91dfb8381e169229e7b1bc57/src/EventSourcing.Test/HandleBadCasesTest.cs) for an example). 

## Concurrency

There is no built-in concurrency handling, events are inserted into the store as they arrive. This is by design, because in our opinion concurrency handling depends on they way you model your streams / aggregates / domain objects. It very often depends on your domain wheter and how to handle conflicts. In our meetup example there is no need to handle concurrent registrations to talks, which might be important with relational persistence to determine who is on the waitlist and who is not.
If validation is performed in a command processor looking at a specific version (version of last applied event) of a projection and you want to make sure state changes are only applied if no one changed this state in the meantime, you might want to persist the 'seen version' on the events produced by the command processor. In the projection you are now able to skip events that where produced based on outdated versions of the aggregate (or even make this conflict visible). This can be implemented for critical events only where domain requires consistency (approval might be an example domain) while events representing a patch of some entity properties might not have the strict requirement to be applied to exactly the version that was seen when issuing the command.
If you prefer not to insert competing events into your store this would have to be handled in a custom persistence implementation.

## Persistence providers
  - [Bluehands.EventSourcing.Persistence.Sqlite]([https://www.nuget.org/packages/Bluehands.EventSourcing.Persistence.Sqlite)
  - [Bluehands.EventSourcing.Persistence.SqlServer]([https://www.nuget.org/packages/Bluehands.EventSourcing.Persistence.SqlServer)

Sqlite and SqlServer persistence packages are based on [Bluehands.EventSourcing.Persistence.EntityFramework](https://www.nuget.org/packages/Bluehands.EventSourcing.Persistence.EntityFramework/) package. We use a very straight forward event store schema here (single table, no normalization). If you're fine with that, it's trivial to use an other entity framework provider to support a new persistence.

To write your own persistence provider, perhaps to have a more optimized storage schema for specific needs or even to abstract from an existing event store like [EventStoreDb](https://www.eventstore.com/) you might want to take [EventSourcing.Persistence.EntityFramework](https://github.com/bluehands/EventSourcing/tree/main/src/EventSourcing.Persistence.EntityFramework) as a template project. Persistence requirements are designed to be minimal:
 - store events in deterministic order 
 - read events from position x
 - read events stream from position x
 
 Those requirements are represented by [`IEventReader`](https://github.com/bluehands/EventSourcing/blob/b285feedd0a18fec91dfb8381e169229e7b1bc57/src/EventSourcing/Infrastructure/Contracts.cs#L7) and [`IEventWriter`](https://github.com/bluehands/EventSourcing/blob/b285feedd0a18fec91dfb8381e169229e7b1bc57/src/EventSourcing/Infrastructure/Contracts.cs#L14) interfaces which a provider has to implement.
 
 ## Contribute
 We are happy to accept pull requests. Feel free to open issues and ask questions. It would be great to have more people contributing to functional event sourcing in .NET!