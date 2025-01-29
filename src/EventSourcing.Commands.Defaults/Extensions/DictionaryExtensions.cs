namespace EventSourcing.Funicular.Commands.Defaults.Extensions;

public static class DictionaryExtensions
{
    public static Result<TValue> Get<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> dictionary,
        TKey key)
        => dictionary.TryGetValue(key, out var value)
            ? Result.Ok(value)
            : Result.Error<TValue>(
                Failure.NotFound($"{typeof(TValue).Name} with key {key} not found."));
}