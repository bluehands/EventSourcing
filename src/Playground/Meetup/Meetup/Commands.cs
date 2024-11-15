using EventSourcing.Funicular.Commands;
using EventSourcing.Funicular.Commands.Extensions;

namespace Meetup;

public record RegisterAttendeeCommand(string TalkId, string AttendeeName, string MailAddress) : Command;

public class RegisterAttendeeCommandProcessor(TalksProjection talksProjection) : SynchronousCommandProcessor<RegisterAttendeeCommand>
{
    public override CommandResult.Processed_ ProcessSync(RegisterAttendeeCommand command)
    {
        var @event = 
            from talk in talksProjection.Current.Talks.Get(command.TalkId)
            from validMail in command.MailAddress.Validate(m => ValidateMail(m, talk))
            select new AttendeeRegistered(command.TalkId, command.AttendeeName, validMail, DateTimeOffset.Now);

        return @event.ToProcessedResult(command);

        IEnumerable<Failure> ValidateMail(string mailAddress, Talk talk)
        {
            if (!mailAddress.Contains("@"))
                yield return Failure.InvalidInput("You need an @");

            if (talk.Attendees.Any(a => a.MailAddress == mailAddress))
                yield return Failure.InvalidInput("Mail address already registered!");
        }
    }
}