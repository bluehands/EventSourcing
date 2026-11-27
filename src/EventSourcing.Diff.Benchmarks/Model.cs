using System.Collections.Immutable;
using EventSourcing.Diff;

namespace EventSourcing.Diff.Benchmarks;

public enum Status { New, Active, Suspended, Closed }

public record Geo(double Latitude, double Longitude, string Region);
public record Address(string Street, string City, string ZipCode, Geo Geo);
public record Profile(string DisplayName, string Email, int Age, Address Address);

public abstract record Payload(string Label);
public record TextPayload(string Label, string Text) : Payload(Label);
public record AmountPayload(string Label, decimal Amount, int Quantity) : Payload(Label);

public record Item(int Id, string Name, decimal Price, bool Enabled);
public record Entry(string Name, int Value, Status Status);
public record Line(int Id, string Description, decimal Amount, int Quantity);

public record State(
    Guid Id,
    string Name,
    string Title,
    int Version,
    int Counter,
    Guid Owner,
    DateTimeOffset Created,
    DateTimeOffset Updated,
    Status Status,
    Status Priority,
    decimal Balance,
    decimal Limit,
    bool IsActive,
    bool IsArchived,
    Profile Profile,
    Payload Payload,
    ImmutableArray<Item> Items,
    ImmutableDictionary<string, Entry> Entries,
    ImmutableArray<Line> Lines);

[Diff(typeof(State))]
[Diff(typeof(Line), MatchBy = nameof(Line.Id))]
public static partial class Comparisons;

public static class StateFactory
{
    public const int ItemCount = 20;
    public const int EntryCount = 20;
    public const int LineCount = 50;

    public static State Create() => new(
        Id: new Guid(1, 0, 0, [0, 0, 0, 0, 0, 0, 0, 1]),
        Name: "Account 4711",
        Title: "Replay state",
        Version: 42,
        Counter: 1000,
        Owner: new Guid(2, 0, 0, [0, 0, 0, 0, 0, 0, 0, 2]),
        Created: new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero),
        Updated: new DateTimeOffset(2026, 6, 7, 8, 9, 10, TimeSpan.Zero),
        Status: Status.Active,
        Priority: Status.New,
        Balance: 1234.56m,
        Limit: 5000m,
        IsActive: true,
        IsArchived: false,
        Profile: new Profile("Alice", "alice@example.com", 33,
            new Address("Main Street 1", "Berlin", "10115", new Geo(52.52, 13.405, "Berlin"))),
        Payload: new AmountPayload("amount", 99.5m, 3),
        Items: [.. Enumerable.Range(0, ItemCount).Select(i => new Item(i, $"Item {i}", i * 1.5m, i % 2 == 0))],
        Entries: Enumerable.Range(0, EntryCount)
            .ToImmutableDictionary(i => $"key{i}", i => new Entry($"Entry {i}", i, (Status)(i % 4))),
        Lines: [.. Enumerable.Range(0, LineCount).Select(i => new Line(1000 + i, $"Line {i}", i * 2.25m, i + 1))]);
}
