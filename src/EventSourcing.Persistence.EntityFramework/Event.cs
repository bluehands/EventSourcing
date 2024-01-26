using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace EventSourcing.Persistence.EntityFramework;

[Index(nameof(StreamType), nameof(StreamId))]
public record Event(
    [property: Key]
    [property: DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    long Position,
    string StreamType,
    string StreamId,
    string EventType,
    string Payload,
    DateTimeOffset Timestamp
);