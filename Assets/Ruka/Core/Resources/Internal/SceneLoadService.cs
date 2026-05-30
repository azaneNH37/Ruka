using System;
using UnityEngine.SceneManagement;

namespace Ruka.Core.Resources
{
    internal sealed class SceneLoadService : ISceneLoadService
    {
        private readonly AssetLoader _assetLoader;

        public SceneLoadService(AssetLoader assetLoader)
        {
            _assetLoader = assetLoader;
        }

        public SceneLoadHandle Load(string address, LoadSceneMode mode, bool suspendLoad = true)
        {
            if (!_assetLoader.IsReady)
            {
                throw new InvalidOperationException("AssetLoader is not ready. Scene loading must start after resource initialization.");
            }

            return _assetLoader.GetPackage().LoadSceneAsync(address, mode, suspendLoad);
        }
    }
}
