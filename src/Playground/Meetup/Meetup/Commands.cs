using EventSourcing.Funicular.Commands;

namespace Meetup;

public record NewUserGroupTalk(string Title, int MaxAttendees) : Command
{
	public string TalkId { get; } = Guid.NewGuid().ToString();
}

public class NewUserGroupTalkCommandProcessor : SynchronousCommandProcessor<NewUserGroupTalk>
{
	public override CommandResult.Processed_ ProcessSync(NewUserGroupTalk command)
	{
		var userGroupTalkAdded = new UserGroupTalkAdded(command.TalkId, command.Title, command.MaxAttendees);
		return command.ToProcessedResult(userGroupTalkAdded, FunctionalResult.Ok($"Talk '{command.Title}' added"));
	}
}

public record RegisterParticipant(string TalkId, string Name, string MailAddress) : Command;

public class RegisterParticipantCommandProcessor(Talks talks) : SynchronousCommandProcessor<RegisterParticipant>
{
	public override CommandResult.Processed_ ProcessSync(RegisterParticipant command)
	{
		var @event =
			from talk in talks.Current.Talks.Get(command.TalkId)
			from validRegistration in talk.Validate(RegistrationOk)
			select new AttendeeRegistered(command.TalkId, command.Name, command.MailAddress, DateTimeOffset.Now);

		return @event.ToProcessedResult(command);

		IEnumerable<Failure> RegistrationOk(Talks.Talk talk)
		{
			if (talk.Attendees.Any(a => a.Name == command.Name))
				yield return Failure.Conflict($"{command.Name} already registered.");
		}
	}
}

public static class DictionaryExtension
{
	public static OperationResult<TValue> Get<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> dict, TKey key)
	{
		if (!dict.TryGetValue(key, out var value))
			return OperationResult.NotFound<TValue>($"{typeof(TValue).Name} with key {key} not found");
		return value;
	}
}