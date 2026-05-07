using System;
using System.Collections.Generic;

namespace Ruka.Utils.Core
{
    public interface IReadOnlyBlackboard
    {
        bool Contains(string key);
        bool TryGet<T>(string key, out T value);
        T GetOrDefault<T>(string key, T defaultValue = default);
        IReadOnlyCollection<string> Keys { get; }
    }

    public sealed class Blackboard : IReadOnlyBlackboard
    {
        private readonly Dictionary<string, object> values = new(StringComparer.Ordinal);

        public IReadOnlyCollection<string> Keys => values.Keys;

        public bool Contains(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            return values.ContainsKey(key);
        }

        public bool TryGet<T>(string key, out T value)
        {
            if (string.IsNullOrEmpty(key))
            {
                value = default;
                return false;
            }

            if (!values.TryGetValue(key, out var rawValue))
            {
                value = default;
                return false;
            }

            if (rawValue is T typed)
            {
                value = typed;
                return true;
            }

            if (rawValue == null)
            {
                value = default;
                return true;
            }

            value = default;
            return false;
        }

        public T GetOrDefault<T>(string key, T defaultValue = default)
        {
            return TryGet(key, out T value) ? value : defaultValue;
        }

        public void Set<T>(string key, T value)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("Key cannot be null or empty.", nameof(key));
            }

            values[key] = value;
        }

        public bool Remove(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            return values.Remove(key);
        }

        public void Clear()
        {
            values.Clear();
        }
    }
}
