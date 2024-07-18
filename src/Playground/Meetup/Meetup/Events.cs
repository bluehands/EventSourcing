using EventSourcing;

namespace Meetup;

public static class StreamIds
{
	public static StreamId UserGroupTalk(string id) => new("UserGroupTalk", id);
}

public static class EventTypes
{
	public const string UserGroupTalkAdded = "UserGroupTalkAdded";
	public const string AttendeeRegistered = "AttendeeRegistered";
}

public interface ITalkEvent
{
	string TalkId { get; }
}

[SerializableEventPayload(EventTypes.UserGroupTalkAdded)] //The 'domain' payload is serialized to the event store directly, use during development and for rapid prototyping only!!
                                                          //See AttendeeRegistered and SerializedPayloads.AttendeeRegistered as an example for separating domain events from serialization concerns.
public record UserGroupTalkAdded(string TalkId, string Title, int MaxAttendees) 
	: EventPayload(StreamIds.UserGroupTalk(TalkId), EventTypes.UserGroupTalkAdded), ITalkEvent;

public record AttendeeRegistered(string TalkId, string Name, string MailAddress, DateTimeOffset RegisteredAt) 
	: EventPayload(StreamIds.UserGroupTalk(TalkId), EventTypes.AttendeeRegistered), ITalkEvent;