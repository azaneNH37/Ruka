using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Ruka.Core.Symbols;
using UnityEngine;
using VContainer;

namespace Ruka.Core.Resources
{
    public interface IAssetScope : IDisposable
    {
        IAssetScope CreateScope();
        UniTask<T> LoadAssetAsync<T>(Symbol<AssetRef> key) where T : UnityEngine.Object;
        UniTask<GameObject> InstantiateAsync(Symbol<AssetRef> key, Func<IObjectResolver, GameObject, GameObject> instantiator = null);
        UniTask<IList<T>> LoadAllByTagAsync<T>(Symbol<AssetTag> tag) where T : UnityEngine.Object;
    }
}
