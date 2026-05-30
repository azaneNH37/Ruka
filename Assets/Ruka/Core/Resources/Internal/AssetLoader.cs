using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Ruka.Core.Resources
{
    internal class AssetLoader : IAssetLoader
    {
        private IAssetPackage _package;
        private readonly UniTaskCompletionSource _readyTcs = new();

        public bool IsReady { get; private set; }

        internal IAssetPackage GetPackage()
        {
            return _package;
        }

        internal UniTask WaitUntilReady()
        {
            return _readyTcs.Task;
        }

        internal void HandleLoadResult(IAssetPackage package, bool success, string errorMessage)
        {
            if (success)
            {
                _package = package;
                IsReady = true;
                _readyTcs.TrySetResult();
                Debug.Log($"[AssetLoader] Initialization completed successfully.");
            }
            else
            {
                var ex = new AssetInitializationException(
                    "DefaultPackage",
                    errorMessage ?? "Unknown initialization error");
                _readyTcs.TrySetException(ex);
                Debug.LogError($"[AssetLoader] Initialization failed: {ex}");
            }
        }
    }
}
