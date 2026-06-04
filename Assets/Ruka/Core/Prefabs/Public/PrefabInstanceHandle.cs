using System;
using System.Threading;
using Ruka.Core.Resources;
using UnityEngine;
using VContainer.Unity;

namespace Ruka.Core.Prefabs
{
    public class PrefabInstanceHandle : IDisposable
    {
        public GameObject GameObject { get; }
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

    public class PrefabInstanceHandle<T> : PrefabInstanceHandle where T : Component
    {
        public T Component { get; }

        internal PrefabInstanceHandle(GameObject gameObject, LifetimeScope scope, IAssetScope assetScope, T component)
            : base(gameObject, scope, assetScope)
        {
            Component = component;
        }
    }
}
