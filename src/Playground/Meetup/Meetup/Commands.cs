using EventSourcing.Commands;

namespace Meetup;

public record NewUserGroupTalk(string Title, int MaxAttendees) : Command
{
	public string TalkId { get; } = Guid.NewGuid().ToString();
}

public class NewUserGroupTalkCommandProcessor : SynchronousCommandProcessor<NewUserGroupTalk>
{
	public override EventSourcing.Commands.ProcessingResult<Failure> ProcessSync(NewUserGroupTalk command)
	{
		var userGroupTalkAdded = new UserGroupTalkAdded(command.TalkId, command.Title, command.MaxAttendees);
        return ProcessingResult.Ok(userGroupTalkAdded);
	}
}

public record RegisterAttendee(string TalkId, string Name, string MailAddress) : Command;

public class RegisterAttendeeCommandProcessor(TalksProjection talks) : SynchronousCommandProcessor<RegisterAttendee>
{
	public override EventSourcing.Commands.ProcessingResult<Failure> ProcessSync(RegisterAttendee command)
    {
        var @event =
            from talk in talks.Current.TalksById.Get(command.TalkId)
            from validRegistration in talk.Validate(RegistrationOk)
            select (
                new AttendeeRegistered(command.TalkId, command.Name, command.MailAddress, DateTimeOffset.Now),
                $"Attendee '{command.Name}' registered for talk '{talk.Title}'"
            );

		return @event.ToProcessingResult();

		IEnumerable<Failure> RegistrationOk(Talk talk)
		{
			if (talk.Attendees.Any(a => a.Name == command.Name))
				yield return Failure.Conflict($"{command.Name} already registered.");
		}
	}
}

public static class DictionaryExtension
{
	public static Result<TValue> Get<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> dict, TKey key)
	{
		if (!dict.TryGetValue(key, out var value))
			return Result.Error<TValue>(Failure.NotFound($"{typeof(TValue).Name} with key {key} not found"));
		return value;
	}
}