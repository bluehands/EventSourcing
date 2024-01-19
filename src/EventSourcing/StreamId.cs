namespace EventSourcing;

public readonly record struct StreamId(string StreamType, string Id);