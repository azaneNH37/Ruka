using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Ruka.Core.Symbols;

namespace Ruka.Core.Resources
{
    public interface IAssetScope : IDisposable
    {
        IAssetScope CreateScope();
        UniTask<T> LoadAssetAsync<T>(Symbol<AssetRef> key) where T : UnityEngine.Object;
        UniTask<IList<T>> LoadAllByTagAsync<T>(Symbol<AssetTag> tag) where T : UnityEngine.Object;
    }
}
