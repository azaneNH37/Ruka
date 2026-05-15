using System;

namespace Ruka.Core.Saves
{
    public readonly struct SaveKey<T> : IEquatable<SaveKey<T>>
    {
        public string Value { get; }

        public SaveKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("SaveKey value cannot be null or whitespace.", nameof(value));
            Value = value;
        }

        public bool Equals(SaveKey<T> other) => Value == other.Value;
        public override bool Equals(object obj) => obj is SaveKey<T> other && Equals(other);
        public override int GetHashCode() => Value?.GetHashCode() ?? 0;
        public static bool operator ==(SaveKey<T> left, SaveKey<T> right) => left.Equals(right);
        public static bool operator !=(SaveKey<T> left, SaveKey<T> right) => !left.Equals(right);
        public override string ToString() => Value;
        public static implicit operator string(SaveKey<T> key) => key.Value;
    }
}
