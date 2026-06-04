using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Ruka.Core.Symbols;
using VContainer;
using Object = UnityEngine.Object;

namespace Ruka.Core.Resources
{
    internal class AssetScope : IAssetScope
    {
        private readonly AssetLoader _loader;
        private readonly IObjectResolver _resolver;
        private readonly HashSet<ReleaseToken> _trackedTokens = new();

        internal AssetScope(AssetLoader loader, IObjectResolver resolver)
        {
            _loader = loader;
            _resolver = resolver;
        }

        public IAssetScope CreateScope()
        {
            return new AssetScope(_loader, _resolver);
        }

        private async UniTask<IAssetPackage> GetPackageReady()
        {
            await _loader.WaitUntilReady();
            return _loader.GetPackage();
        }

        public async UniTask<T> LoadAssetAsync<T>(Symbol<AssetRef> key) where T : Object
        {
            var package = await GetPackageReady();
            var (asset, token) = await package.LoadAssetAsync<T>(key);
            _trackedTokens.Add(token);
            return asset;
        }

        public async UniTask<IList<T>> LoadAllByTagAsync<T>(Symbol<AssetTag> tag) where T : Object
        {
            var package = await GetPackageReady();
            var results = await package.LoadAllByTagAsync<T>(tag);
            for (var i = 0; i < results.Count; i++)
            {
                _trackedTokens.Add(results[i].token);
            }

            var assets = new List<T>(results.Count);
            for (var i = 0; i < results.Count; i++)
            {
                assets.Add(results[i].asset);
            }

            return assets;
        }

        internal void UntrackToken(ReleaseToken token)
        {
            _trackedTokens.Remove(token);
        }

        internal void ReleaseToken(ReleaseToken token)
        {
            if (_trackedTokens.Remove(token))
            {
                var package = _loader.GetPackage();
                package?.Release(token);
            }
        }

        public void Dispose()
        {
            var package = _loader.GetPackage();
            if (package != null)
            {
                foreach (var token in _trackedTokens)
                {
                    package.Release(token);
                }
            }

            _trackedTokens.Clear();
        }
    }
}
