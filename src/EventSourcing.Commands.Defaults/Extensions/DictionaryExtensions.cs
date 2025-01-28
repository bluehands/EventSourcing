namespace EventSourcing.Funicular.Commands.Defaults.Extensions;

public static class DictionaryExtensions
{
    public static OperationResult<TValue> Get<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> dictionary,
        TKey key)
        => dictionary.TryGetValue(key, out var value)
            ? OperationResult.Ok(value)
            : OperationResult.Error<TValue>(
                Failure.NotFound($"{typeof(TValue).Name} with key {key} not found."));
}