using System;

namespace Ruka.Core.Pool
{
    /// <summary>
    /// Unified pool access contract for high-frequency acquire/return workflows.
    /// </summary>
    public interface IObjectPool<T> : IDisposable
    {
        /// <summary>
        /// Acquires an instance from the pool, creating one if the inactive stack is empty.
        /// </summary>
        T Get();

        /// <summary>
        /// Returns an instance to the pool.
        /// </summary>
        void Return(T instance);

        /// <summary>
        /// Number of instances currently acquired via Get and not yet returned.
        /// </summary>
        int CountActive { get; }

        /// <summary>
        /// Number of instances sitting in the inactive stack, available for Get.
        /// </summary>
        int CountInactive { get; }
    }
}
