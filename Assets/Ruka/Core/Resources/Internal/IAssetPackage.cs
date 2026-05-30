using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ruka.Core.Resources
{
    internal interface IAssetPackage
    {
        UniTask<(T asset, ReleaseToken token)> LoadAssetAsync<T>(string address) where T : Object;
        UniTask<(GameObject prefab, ReleaseToken token)> LoadPrefabAsync(string address);
        UniTask<IList<(T asset, ReleaseToken token)>> LoadAllByTagAsync<T>(string tag) where T : Object;
        SceneLoadHandle LoadSceneAsync(string address, LoadSceneMode mode, bool suspendLoad);
        void Release(ReleaseToken token);
    }
}
