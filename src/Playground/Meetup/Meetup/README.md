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

Open your browser and navigate to http://localhost:60868/graphql an play around with the api:
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

