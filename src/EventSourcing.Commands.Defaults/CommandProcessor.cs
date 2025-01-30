namespace EventSourcing.Funicular.Commands.Defaults;

public abstract class CommandProcessor<T> : CommandProcessor<T, Failure>
    where T : Command
{
}

public abstract class SynchronousCommandProcessor<T> : SynchronousCommandProcessor<T, Failure>
    where T : Command
{
}