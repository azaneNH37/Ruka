using System;

namespace Ruka.Core.Pool
{
    /// <summary>
    /// Thrown when a fixed-size pool has no inactive instances remaining.
    /// </summary>
    public sealed class PoolCapacityExceededException : Exception
    {
        public PoolCapacityExceededException(string message)
            : base(message)
        {
        }
    }
}
