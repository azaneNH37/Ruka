using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using YooAsset;

namespace Ruka.Core.Resources
{
    internal sealed class PackageAdapter : IAssetPackage
    {
        private readonly ResourcePackage _package;
        private readonly Dictionary<int, AssetHandle> _assetHandles = new();
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

        public SceneLoadHandle LoadSceneAsync(string address, LoadSceneMode mode, bool suspendLoad)
        {
            var handle = _package.LoadSceneAsync(address, sceneMode: mode, suspendLoad: suspendLoad);

            return new SceneLoadHandle(
                progressFunc: () => handle.Progress,
                isDoneFunc: () => handle.IsDone,
                sceneObjectFunc: () => handle.SceneObject,
                activateAction: () =>
                {
                    if (suspendLoad)
                    {
                        handle.UnSuspend();
                        return;
                    }

                    handle.ActivateScene();
                },
                unloadFunc: () => handle.UnloadAsync().ToUniTask());
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
        }

        private ReleaseToken AllocateToken()
        {
            var token = Interlocked.Increment(ref _nextToken);
            return new ReleaseToken(token - 1);
        }
    }
}
