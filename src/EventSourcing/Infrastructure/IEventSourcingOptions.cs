using System.Collections.Generic;

namespace EventSourcing.Infrastructure;

public interface IEventSourcingOptions
{
    IEnumerable<IEventSourcingOptionsExtension> Extensions { get; }
}