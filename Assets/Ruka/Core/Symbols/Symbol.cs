using System;
using UnityEngine;
using Ruka.Utils.Core;

namespace Ruka.Core.Symbols
{
    /// <summary>
    /// Type-safe string ID parameterized by a marker struct. Not a replacement for enum — use when IDs must be serializable, Inspector-selectable, and scoped to a specific domain type.
    /// </summary>
    [Serializable]
    public struct Symbol<T> : IEquatable<Symbol<T>>, ISerializationCallbackReceiver where T : struct
    {
        [SerializeField] private string value;
        [NonSerialized] private int hash;
        [NonSerialized] private bool hashInitialized;

        /// <summary>Constructs a Symbol wrapping the given string value.</summary>
        public Symbol(string value)
        {
            this.value = value ?? string.Empty;
            hash = 0;
            hashInitialized = false;
        }

        /// <summary>The underlying string value. Returns empty string when unset or null.</summary>
        public string Value => value ?? string.Empty;

        /// <summary>Lazily computed FNV-1a hash of Value. Always reflects the current Value; recomputed after Unity deserialization.</summary>
        public int Hash
        {
            get
            {
                EnsureHashInitialized();
                return hash;
            }
        }

        /// <summary>True when Value is null or empty.</summary>
        public bool IsEmpty => string.IsNullOrEmpty(Value);

        public void OnBeforeSerialize() { }

        public void OnAfterDeserialize()
        {
            hashInitialized = false;
            hash = 0;
        }

        public override string ToString() => Value;
        public override int GetHashCode() => Hash;
        public override bool Equals(object obj) => obj is Symbol<T> other && Equals(other);

        public bool Equals(Symbol<T> other)
        {
            if (Hash != other.Hash)
            {
                return false;
            }

            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public static bool operator ==(Symbol<T> left, Symbol<T> right) => left.Equals(right);
        public static bool operator !=(Symbol<T> left, Symbol<T> right) => !left.Equals(right);
        public static implicit operator string(Symbol<T> symbol) => symbol.Value;

        private void EnsureHashInitialized()
        {
            if (hashInitialized)
            {
                return;
            }

            hash = string.IsNullOrEmpty(Value) ? 0 : Value.GetStableHash();
            hashInitialized = true;
        }
    }
}
