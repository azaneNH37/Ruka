using System;
using System.Collections.Generic;

namespace Ruka.Utils.Core
{
    public static class DictionaryExtensions
    {
        public static TValue GetValueOrDefault<TKey, TValue>(
            this IReadOnlyDictionary<TKey, TValue> dict,
            TKey key,
            TValue defaultValue = default)
        {
            if (dict == null)
            {
                throw new ArgumentNullException(nameof(dict));
            }

            return dict.TryGetValue(key, out var value) ? value : defaultValue;
        }

        public static TValue GetOrAdd<TKey, TValue>(
            this IDictionary<TKey, TValue> dict,
            TKey key,
            Func<TValue> factory)
        {
            if (dict == null)
            {
                throw new ArgumentNullException(nameof(dict));
            }

            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            if (dict.TryGetValue(key, out var value))
            {
                return value;
            }

            value = factory();
            dict[key] = value;
            return value;
        }
    }
}
