using System;

namespace Ruka.Core.Resources
{
    internal readonly struct ReleaseToken : IEquatable<ReleaseToken>
    {
        public readonly int Value;

        internal ReleaseToken(int value)
        {
            Value = value;
        }

        public bool Equals(ReleaseToken other) => Value == other.Value;
        public override bool Equals(object obj) => obj is ReleaseToken other && Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => Value.ToString();
    }
}
