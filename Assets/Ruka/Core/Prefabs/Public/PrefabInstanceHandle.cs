using System;
using System.Threading;
using Ruka.Core.Resources;
using UnityEngine;
using VContainer.Unity;

namespace Ruka.Core.Prefabs
{
    /// <summary>
    /// Owns a prefab instance and its asset reference. Disposing destroys the GO
    /// and releases the loaded asset. Idempotent — safe to call multiple times.
    /// </summary>
    public class PrefabInstanceHandle : IDisposable
    {
        /// <summary>The instantiated root GameObject.</summary>
        public GameObject GameObject { get; }

        /// <summary>The child <see cref="LifetimeScope"/> on the instance root, or null for plain (non-scope) prefabs.</summary>
        public LifetimeScope Scope { get; }

        private readonly IAssetScope _assetScope;
        private CancellationTokenRegistration _ctRegistration;
        private bool _disposed;

        internal PrefabInstanceHandle(GameObject gameObject, LifetimeScope scope, IAssetScope assetScope)
        {
            GameObject = gameObject;
            Scope = scope;
            _assetScope = assetScope;
        }

        internal void BindLifetime(CancellationToken ct)
        {
            if (ct.CanBeCanceled)
                _ctRegistration = ct.Register(static state => ((PrefabInstanceHandle)state).Dispose(), this);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _ctRegistration.Dispose();

            if (GameObject != null)
                UnityEngine.Object.Destroy(GameObject);

            _assetScope?.Dispose();
        }
    }

    /// <summary>
    /// Typed variant that also exposes the root component of type <typeparamref name="T"/>.
    /// </summary>
    public class PrefabInstanceHandle<T> : PrefabInstanceHandle where T : Component
    {
        /// <summary>The component on the instance root, guaranteed non-null at construction.</summary>
        public T Component { get; }

        internal PrefabInstanceHandle(GameObject gameObject, LifetimeScope scope, IAssetScope assetScope, T component)
            : base(gameObject, scope, assetScope)
        {
            Component = component;
        }
    }
}
