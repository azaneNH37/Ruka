using System;
using UnityEngine;

namespace Ruka.Utils.Core
{
    [Serializable]
    public sealed class SerializableType : ISerializationCallbackReceiver, IEquatable<SerializableType>
    {
        [SerializeField] private string assemblyQualifiedName;
        [NonSerialized] private Type type;

        public Type Type => type;
        public bool IsValid => type != null;

        public void Set(Type type)
        {
            this.type = type;
            assemblyQualifiedName = type?.AssemblyQualifiedName;
        }

        public void OnBeforeSerialize()
        {
            assemblyQualifiedName = type?.AssemblyQualifiedName;
        }

        public void OnAfterDeserialize()
        {
            if (string.IsNullOrEmpty(assemblyQualifiedName))
            {
                type = null;
                return;
            }

            type = Type.GetType(assemblyQualifiedName);
        }

        public bool Equals(SerializableType other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return string.Equals(assemblyQualifiedName, other.assemblyQualifiedName, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is SerializableType other && Equals(other);
        }

        public override int GetHashCode()
        {
            return assemblyQualifiedName != null ? assemblyQualifiedName.GetHashCode() : 0;
        }
    }
}
