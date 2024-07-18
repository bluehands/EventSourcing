using EventSourcing;

namespace Meetup.SerializedPayloads;

[SerializableEventPayload(EventTypes.AttendeeRegistered)] //this has to be kept stable / compatible (regarding the selected serializer) because it is serialized to and deserialized from the event store
public record AttendeeRegistered(string Name, string MailAddress, DateTime RegisteredAt, double RegisteredAtOffset); //RegisteredAt + RegisteredAtOffset is just an example of a serializable
                                                                                                                     //representation that differs from the type used at domain layer (DateTimeOffset).
                                                                                                                     //Of course ths System.Text.Json serializer used in our example would support using DateTimeOffset directly.

public class AttendeeRegisteredMapper : EventPayloadMapper<Meetup.AttendeeRegistered, AttendeeRegistered>
{
    protected override Meetup.AttendeeRegistered MapFromSerializablePayload(AttendeeRegistered serialized, StreamId streamId) => 
        new (streamId.Id, serialized.Name, serialized.MailAddress,  new (serialized.RegisteredAt, TimeSpan.FromHours(serialized.RegisteredAtOffset)));

    protected override AttendeeRegistered MapToSerializablePayload(Meetup.AttendeeRegistered payload) => 
        new (payload.Name, payload.MailAddress, payload.RegisteredAt.DateTime, payload.RegisteredAt.Offset.TotalHours);
}