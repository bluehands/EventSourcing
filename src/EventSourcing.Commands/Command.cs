namespace EventSourcing.Funicular.Commands;

public abstract record Command
{
    public CommandId Id { get; } = CommandId.NewCommandId();

    public override string ToString() => $"{GetType().Name} ({Id.Id})";
}