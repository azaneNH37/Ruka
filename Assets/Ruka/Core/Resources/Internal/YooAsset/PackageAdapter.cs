using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using YooAsset;

namespace Ruka.Core.Resources
{
    internal sealed class PackageAdapter : IAssetPackage
    {
        private readonly ResourcePackage _package;
        private readonly Dictionary<int, AssetHandle> _assetHandles = new();
        private readonly Dictionary<int, SceneHandle> _sceneHandles = new();
        private int _nextToken;

        public PackageAdapter(ResourcePackage package)
        {
            _package = package;
        }

        public async UniTask<(T asset, ReleaseToken token)> LoadAssetAsync<T>(string address) where T : UnityEngine.Object
        {
            var handle = _package.LoadAssetAsync<T>(address);
            await handle.ToUniTask();

            if (handle.Status != EOperationStatus.Succeed)
            {
                var error = handle.LastError;
                handle.Release();
                throw new AssetLoadException(address, error);
            }

            var token = AllocateToken();
            _assetHandles[token.Value] = handle;
            return ((T)handle.AssetObject, token);
        }

        public async UniTask<(GameObject prefab, ReleaseToken token)> LoadPrefabAsync(string address)
        {
            var handle = _package.LoadAssetAsync<GameObject>(address);
            await handle.ToUniTask();

            if (handle.Status != EOperationStatus.Succeed)
            {
                var error = handle.LastError;
                handle.Release();
                throw new AssetLoadException(address, error);
            }

            var token = AllocateToken();
            _assetHandles[token.Value] = handle;
            return (handle.AssetObject as GameObject, token);
        }

        public async UniTask<IList<(T asset, ReleaseToken token)>> LoadAllByTagAsync<T>(string tag) where T : UnityEngine.Object
        {
            var assetInfos = _package.GetAssetInfos(tag);
            if (assetInfos.Length == 0)
            {
                return new List<(T, ReleaseToken)>();
            }

            var handles = new List<AssetHandle>(assetInfos.Length);
            var tasks = new List<UniTask>(assetInfos.Length);

            for (var i = 0; i < assetInfos.Length; i++)
            {
                var handle = _package.LoadAssetAsync<T>(assetInfos[i].Address);
                handles.Add(handle);
                tasks.Add(handle.ToUniTask());
            }

            await UniTask.WhenAll(tasks);

            var result = new List<(T asset, ReleaseToken token)>(assetInfos.Length);
            List<string> errors = null;

            for (var i = 0; i < handles.Count; i++)
            {
                var handle = handles[i];
                if (handle.Status == EOperationStatus.Succeed)
                {
                    var token = AllocateToken();
                    _assetHandles[token.Value] = handle;
                    result.Add(((T)(object)handle.AssetObject, token));
                }
                else
                {
                    errors ??= new List<string>();
                    errors.Add(handle.LastError);
                    handle.Release();
                }
            }

            if (errors != null)
            {
                throw new AssetLoadException($"<Tag>{tag}", string.Join(", ", errors));
            }

            return result;
        }

        public SceneResHandle LoadSceneAsync(string address)
        {
            var handle = _package.LoadSceneAsync(address);

            var token = AllocateToken();
            _sceneHandles[token.Value] = handle;

            return new SceneResHandle(
                progressFunc: () => handle.Progress,
                isLoadedFunc: () => handle.IsDone,
                activateFunc: async () =>
                {
                    if (!handle.IsDone)
                        await handle.ToUniTask();
                    _sceneHandles.Remove(token.Value);
                    handle.ActivateScene();
                });
        }

        public void Release(ReleaseToken token)
        {
            if (_assetHandles.TryGetValue(token.Value, out var assetHandle))
            {
                _assetHandles.Remove(token.Value);
                if (assetHandle.IsValid)
                {
                    assetHandle.Release();
                }
            }

            if (_sceneHandles.TryGetValue(token.Value, out var sceneHandle))
            {
                _sceneHandles.Remove(token.Value);
            }
        }

        private ReleaseToken AllocateToken()
        {
            var token = Interlocked.Increment(ref _nextToken);
            return new ReleaseToken(token - 1);
        }
    }
}
