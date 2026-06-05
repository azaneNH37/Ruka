using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Ruka.Core.Resources;
using Ruka.Core.Symbols;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace Ruka.Core.Prefabs
{
    internal sealed class PrefabFactory : IPrefabFactory
    {
        private readonly IAssetScope _assetScope;
        private readonly IObjectResolver _resolver;
        private readonly LifetimeScope _scope;

        [Inject]
        public PrefabFactory(IAssetScope assetScope, IObjectResolver resolver, LifetimeScope scope)
        {
            _assetScope = assetScope;
            _resolver = resolver;
            _scope = scope;
        }

        public async UniTask<PrefabInstanceHandle> InstantiateAsync(
            Symbol<AssetRef> key,
            Action<PrefabOptions> configure = null,
            CancellationToken ct = default)
        {
            var (instance, childScope, instanceAssetScope, effectiveCt) =
                await RunPipeline(key, configure, ct);

            var handle = new PrefabInstanceHandle(instance, childScope, instanceAssetScope);
            FinishHandle(handle, instance, effectiveCt);
            return handle;
        }

        public async UniTask<PrefabInstanceHandle<T>> InstantiateAsync<T>(
            Symbol<AssetRef> key,
            Action<PrefabOptions> configure = null,
            CancellationToken ct = default)
            where T : Component
        {
            var (instance, childScope, instanceAssetScope, effectiveCt) =
                await RunPipeline(key, configure, ct);

            if (!instance.TryGetComponent<T>(out var component))
            {
                Object.Destroy(instance);
                instanceAssetScope.Dispose();
                throw new InvalidOperationException(
                    $"Prefab '{key}' does not have component {typeof(T).Name} on its root GameObject.");
            }

            var handle = new PrefabInstanceHandle<T>(instance, childScope, instanceAssetScope, component);
            FinishHandle(handle, instance, effectiveCt);
            return handle;
        }

        private async UniTask<(GameObject instance, LifetimeScope childScope, IAssetScope instanceAssetScope, CancellationToken effectiveCt)>
            RunPipeline(Symbol<AssetRef> key, Action<PrefabOptions> configure, CancellationToken ct)
        {
            var options = new PrefabOptions();
            configure?.Invoke(options);

            var diParent = options.DiParentOverride != null ? options.DiParentOverride : _scope;
            var effectiveCt = ct == default ? diParent.destroyCancellationToken : ct;
            effectiveCt.ThrowIfCancellationRequested();

            var instanceAssetScope = _assetScope.CreateScope();
            GameObject instance = null;
            LifetimeScope childScope = null;

            try
            {
                var prefab = await instanceAssetScope.LoadAssetAsync<GameObject>(key);
                effectiveCt.ThrowIfCancellationRequested();

                var wasActive = prefab.activeSelf;
                if (wasActive) prefab.SetActive(false);

                try
                {
                    instance = InstantiateRaw(prefab, options);

                    childScope = InjectPrefabTree(instance, diParent, options, out var activatedByScope);

                    if (!activatedByScope && !options.IsManualActivation)
                        instance.SetActive(true);
                }
                finally
                {
                    if (wasActive) prefab.SetActive(true);
                }

                return (instance, childScope, instanceAssetScope, effectiveCt);
            }
            catch
            {
                if (instance != null) Object.Destroy(instance);
                instanceAssetScope.Dispose();
                throw;
            }
        }

        private LifetimeScope InjectPrefabTree(
            GameObject root, LifetimeScope diParent, PrefabOptions options, out bool activatedByScope)
        {
            if (root.TryGetComponent<LifetimeScope>(out var rootScope))
            {
                rootScope.parentReference.Object = diParent;
                activatedByScope = !options.IsManualActivation;

                if (activatedByScope)
                {
                    if (options.Installers != null)
                    {
                        using var _ = EnqueueInstallers(options.Installers);
                        root.SetActive(true);
                    }
                    else
                    {
                        root.SetActive(true);
                    }
                }

                return rootScope;
            }

            activatedByScope = false;
            var resolver = diParent.Container ?? _resolver;
            InjectGameObjectScopeBounded(root.transform, resolver, diParent);
            return null;
        }

        private static void InjectGameObjectScopeBounded(Transform current, IObjectResolver resolver, LifetimeScope diParent)
        {
            var components = current.GetComponents<MonoBehaviour>();
            foreach (var mb in components)
                if (mb != null) resolver.Inject(mb);

            for (var i = 0; i < current.childCount; i++)
            {
                var child = current.GetChild(i);

                if (child.TryGetComponent<LifetimeScope>(out var childScope))
                {
                    childScope.parentReference.Object = diParent;
                    continue;
                }

                InjectGameObjectScopeBounded(child, resolver, diParent);
            }
        }

        private static GameObject InstantiateRaw(GameObject prefab, PrefabOptions options)
        {
            if (options.Position.HasValue)
            {
                var pos = options.Position.Value;
                var rot = options.Rotation ?? prefab.transform.rotation;

                return options.VisualParent != null
                    ? Object.Instantiate(prefab, pos, rot, options.VisualParent)
                    : Object.Instantiate(prefab, pos, rot);
            }

            if (options.VisualParent != null)
                return Object.Instantiate(prefab, options.VisualParent, options.IsWorldPositionStays);

            return Object.Instantiate(prefab);
        }

        private static LifetimeScope.ExtraInstallationScope EnqueueInstallers(List<IInstaller> installers)
        {
            return LifetimeScope.Enqueue(new CompositeInstaller(installers));
        }

        private static void FinishHandle(
            PrefabInstanceHandle handle,
            GameObject instance,
            CancellationToken effectiveCt)
        {
            var hook = instance.AddComponent<PrefabReleaseHook>();
            hook.Init(handle);

            handle.BindLifetime(effectiveCt);
        }
    }
}
