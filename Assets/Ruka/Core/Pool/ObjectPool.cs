using System;
using System.Collections.Generic;

namespace Ruka.Core.Pool
{
    /// <summary>
    /// Delegate-driven object pool for plain C# types.
    /// </summary>
    public class ObjectPool<T> : IObjectPool<T>
    {
        private readonly Func<T> _create;
        private readonly Action<T> _onCreated;
        private readonly Action<T> _onReturn;
        private readonly Action<T> _onDestroy;
        private readonly Stack<T> _inactive;
        private readonly PoolSettings _settings;
        private int _countAll;
        private int _countActive;
        private bool _disposed;

        public ObjectPool(
            Func<T> create,
            Action<T> onReturn = null,
            Action<T> onDestroy = null,
            PoolSettings settings = default)
            : this(create, null, onReturn, onDestroy, settings)
        {
        }

        protected ObjectPool(
            Func<T> create,
            Action<T> onCreated,
            Action<T> onReturn,
            Action<T> onDestroy,
            PoolSettings settings)
        {
            _create = create ?? throw new ArgumentNullException(nameof(create));
            _onCreated = onCreated;
            _onReturn = onReturn;
            _onDestroy = onDestroy;
            _settings = Normalize(settings);
            _inactive = new Stack<T>(_settings.InitialSize);

            Prewarm(_settings.InitialSize);
        }

        public int CountActive => _countActive;

        public int CountInactive => _inactive.Count;

        public virtual T Get()
        {
            ThrowIfDisposed();

            T instance;
            if (_inactive.Count > 0)
            {
                instance = _inactive.Pop();
            }
            else
            {
                if (_settings.FixedTotalSize.HasValue && _countAll >= _settings.FixedTotalSize.Value)
                {
                    throw new PoolCapacityExceededException(
                        $"Pool<{typeof(T).Name}> reached fixed total size {_settings.FixedTotalSize.Value}.");
                }

                instance = CreateInstance();
            }

            _countActive++;
            return instance;
        }

        public virtual void Return(T instance)
        {
            ThrowIfDisposed();
            ThrowIfNullReference(instance, nameof(instance));

            if (_countActive <= 0)
            {
                throw new InvalidOperationException($"Pool<{typeof(T).Name}> received more returned instances than acquired instances.");
            }

            if (_settings.CollectionCheck && _inactive.Contains(instance))
            {
                throw new InvalidOperationException($"Pool<{typeof(T).Name}> received an instance that is already inactive.");
            }

            ResetInstance(instance);
            _onReturn?.Invoke(instance);
            _countActive--;

            if (ShouldDestroyOnReturn())
            {
                DestroyInstance(instance);
                return;
            }

            _inactive.Push(instance);
        }

        public virtual void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            while (_inactive.Count > 0)
            {
                DestroyInstance(_inactive.Pop());
            }

            _disposed = true;
        }

        private static PoolSettings Normalize(PoolSettings settings)
        {
            if (settings.Equals(default(PoolSettings)))
            {
                settings = PoolSettings.Default;
            }

            if (settings.InitialSize < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(settings), "InitialSize must be greater than or equal to zero.");
            }

            if (settings.MaxInactiveSize < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(settings), "MaxInactiveSize must be greater than or equal to zero.");
            }

            if (!settings.FixedTotalSize.HasValue && settings.MaxInactiveSize == 0)
            {
                settings.MaxInactiveSize = int.MaxValue;
            }

            if (settings.FixedTotalSize < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(settings), "FixedTotalSize must be greater than or equal to zero.");
            }

            if (settings.FixedTotalSize.HasValue && settings.InitialSize > settings.FixedTotalSize.Value)
            {
                throw new ArgumentOutOfRangeException(nameof(settings), "InitialSize must be less than or equal to FixedTotalSize.");
            }

            return settings;
        }

        private void Prewarm(int count)
        {
            for (var i = 0; i < count; i++)
            {
                _inactive.Push(CreateInstance());
            }
        }

        private T CreateInstance()
        {
            var instance = _create();
            ThrowIfNullReference(instance, nameof(_create));
            _onCreated?.Invoke(instance);
            _countAll++;
            return instance;
        }

        private static void ThrowIfNullReference(T instance, string paramName)
        {
            if (!typeof(T).IsValueType && instance == null)
            {
                throw new ArgumentNullException(paramName);
            }
        }

        private void ResetInstance(T instance)
        {
            if (instance is IResettable resettable)
            {
                resettable.ResetState();
            }
        }

        private bool ShouldDestroyOnReturn()
        {
            return !_settings.FixedTotalSize.HasValue && _inactive.Count >= _settings.MaxInactiveSize;
        }

        private void DestroyInstance(T instance)
        {
            _onDestroy?.Invoke(instance);
            _countAll--;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }
        }
    }
}
