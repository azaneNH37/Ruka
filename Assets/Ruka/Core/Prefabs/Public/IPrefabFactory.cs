using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Ruka.Core.Resources;
using Ruka.Core.Symbols;
using UnityEngine;

namespace Ruka.Core.Prefabs
{
    /// <summary>
    /// Context-aware prefab instantiation pipeline. Not a replacement for
    /// <c>Object.Instantiate</c> — use this when the instance needs DI injection,
    /// scoped asset tracking, or a visual parent that differs from the DI parent.
    /// </summary>
    public interface IPrefabFactory
    {
        /// <summary>
        /// Loads and instantiates a prefab, returning an ownership handle.
        /// </summary>
        /// <param name="key">YooAsset address of the prefab.</param>
        /// <param name="configure">Optional callback to set visual parent, DI parent, installers, position, etc.</param>
        /// <param name="ct">
        /// Cancels the async load when pending; after instantiation, binds instance
        /// lifetime so the GO is destroyed when the token fires.
        /// Defaults to the DI parent's <c>destroyCancellationToken</c> (the overridden scope
        /// if <see cref="PrefabOptions.WithDiParent"/> is set, otherwise the factory's owning scope).
        /// Never defaults to <c>CancellationToken.None</c>.
        /// </param>
        UniTask<PrefabInstanceHandle> InstantiateAsync(
            Symbol<AssetRef> key,
            Action<PrefabOptions> configure = null,
            CancellationToken ct = default);

        /// <summary>
        /// Loads and instantiates a prefab, returning a typed handle with the root component.
        /// </summary>
        /// <remarks>Throws <see cref="InvalidOperationException"/> if the root GO lacks component <typeparamref name="T"/>.</remarks>
        UniTask<PrefabInstanceHandle<T>> InstantiateAsync<T>(
            Symbol<AssetRef> key,
            Action<PrefabOptions> configure = null,
            CancellationToken ct = default)
            where T : Component;
    }
}
