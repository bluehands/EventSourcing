using System;

namespace EventSourcing.Commands;

public record CommandId(Guid Id)
{
    public static CommandId NewCommandId() => new(Guid.NewGuid());
    public override string ToString() => Id.ToString("N");
}