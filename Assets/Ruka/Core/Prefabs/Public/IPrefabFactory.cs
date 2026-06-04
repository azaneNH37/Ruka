using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Ruka.Core.Resources;
using Ruka.Core.Symbols;
using UnityEngine;

namespace Ruka.Core.Prefabs
{
    public interface IPrefabFactory
    {
        UniTask<PrefabInstanceHandle> InstantiateAsync(
            Symbol<AssetRef> key,
            Action<PrefabOptions> configure = null,
            CancellationToken ct = default);

        UniTask<PrefabInstanceHandle<T>> InstantiateAsync<T>(
            Symbol<AssetRef> key,
            Action<PrefabOptions> configure = null,
            CancellationToken ct = default)
            where T : Component;
    }
}
