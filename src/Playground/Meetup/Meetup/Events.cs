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

[SerializableEventPayload(EventTypes.UserGroupTalkAdded)]
public record UserGroupTalkAdded(string TalkId, string Title, int MaxAttendees) 
	: EventPayload(StreamIds.UserGroupTalk(TalkId), EventTypes.UserGroupTalkAdded), ITalkEvent;

[SerializableEventPayload(EventTypes.AttendeeRegistered)]
public record AttendeeRegistered(string TalkId, string Name, string MailAddress, DateTimeOffset RegisteredAt) 
	: EventPayload(StreamIds.UserGroupTalk(TalkId), EventTypes.AttendeeRegistered), ITalkEvent;