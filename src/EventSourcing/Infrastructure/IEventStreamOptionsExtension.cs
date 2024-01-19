namespace EventSourcing.Infrastructure;

/// <summary>
/// Mark OptionsExtensions that provide an event stream with this interface, so persistence layers can provide their default
/// event streams if no extension of type IEventStreamOptionsExtension is found after configuration.
/// </summary>
public interface IEventStreamOptionsExtension : IEventSourcingOptionsExtension;