using System.Collections.Generic;

namespace EventSourcing.Funicular.Commands.Extensions;

public static class DictionaryExtension
{
    public static OperationResult<TValue> Get<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> dict, TKey key) =>
        !dict.TryGetValue(key, out var value) 
            ? OperationResult.NotFound<TValue>($"{typeof(TValue).Name} with key {key} not found.") 
            : value;
}
