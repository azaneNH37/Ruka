using System;
using UnityEngine;
using Ruka.Utils.Core;

namespace Ruka.Core.Symbols
{
    [Serializable]
    public struct Symbol<T> : IEquatable<Symbol<T>>, ISerializationCallbackReceiver where T : struct
    {
        [SerializeField] private string value;
        [NonSerialized] private int hash;
        [NonSerialized] private bool hashInitialized;

        public Symbol(string value)
        {
            this.value = value ?? string.Empty;
            hash = 0;
            hashInitialized = false;
        }

        public string Value => value ?? string.Empty;
        public int Hash
        {
            get
            {
                EnsureHashInitialized();
                return hash;
            }
        }

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
