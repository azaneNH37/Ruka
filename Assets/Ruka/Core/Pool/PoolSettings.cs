using System;

namespace Ruka.Core.Pool
{
    /// <summary>
    /// Controls pool capacity and validation behavior.
    /// </summary>
    public struct PoolSettings : IEquatable<PoolSettings>
    {
        /// <summary>
        /// Instances pre-created at construction time.
        /// </summary>
        public int InitialSize;

        /// <summary>
        /// Maximum number of inactive instances retained in the stack.
        /// </summary>
        public int MaxInactiveSize;

        /// <summary>
        /// Hard cap on total instances ever created.
        /// </summary>
        public int? FixedTotalSize;

        /// <summary>
        /// Enables duplicate-return detection against the inactive stack.
        /// </summary>
        public bool CollectionCheck;

        public static readonly PoolSettings Default = new PoolSettings
        {
            InitialSize = 0,
            MaxInactiveSize = int.MaxValue,
            FixedTotalSize = null,
#if DEBUG
            CollectionCheck = true
#else
            CollectionCheck = false
#endif
        };

        public bool Equals(PoolSettings other)
        {
            return InitialSize == other.InitialSize
                && MaxInactiveSize == other.MaxInactiveSize
                && FixedTotalSize == other.FixedTotalSize
                && CollectionCheck == other.CollectionCheck;
        }

        public override bool Equals(object obj)
        {
            return obj is PoolSettings other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = InitialSize;
                hashCode = (hashCode * 397) ^ MaxInactiveSize;
                hashCode = (hashCode * 397) ^ FixedTotalSize.GetHashCode();
                hashCode = (hashCode * 397) ^ CollectionCheck.GetHashCode();
                return hashCode;
            }
        }
    }
}
