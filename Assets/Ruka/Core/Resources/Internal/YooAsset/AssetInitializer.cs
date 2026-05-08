using System.Diagnostics;
using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;
using VContainer.Unity;
using YooAsset;
using Debug = UnityEngine.Debug;

namespace Ruka.Core.Resources
{
    internal class AssetInitializer : IAsyncStartable
    {
        private readonly ResourceConfig _config;
        private readonly AssetLoader _loader;

        public AssetInitializer(ResourceConfig config, AssetLoader loader)
        {
            _config = config;
            _loader = loader;
        }

        public async UniTask StartAsync(CancellationToken cancellation = default)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            YooAssets.Initialize();
            var package = YooAssets.CreatePackage(_config.PackageName);
            YooAssets.SetDefaultPackage(package);

            InitializeParameters initParameters;
            if (_config.Mode == PlayMode.EditorSimulateMode)
            {
                var buildResult = EditorSimulateModeHelper.SimulateBuild(_config.PackageName);
                var packageRoot = buildResult.PackageRootDirectory;
                var fileSystemParams = FileSystemParameters.CreateDefaultEditorFileSystemParameters(packageRoot);
                initParameters = new EditorSimulateModeParameters
                {
                    EditorFileSystemParameters = fileSystemParams
                };
            }
            else
            {
                var fileSystemParams = FileSystemParameters.CreateDefaultBuildinFileSystemParameters();
                initParameters = new OfflinePlayModeParameters
                {
                    BuildinFileSystemParameters = fileSystemParams
                };
            }

            var initOperation = package.InitializeAsync(initParameters);
            await initOperation.ToUniTask();

            if (initOperation.Status != EOperationStatus.Succeed)
            {
                stopwatch.Stop();
                _loader.HandleLoadResult(null, false, initOperation.Error);
                Debug.LogError($"[AssetInitializer] Package initialization failed: {initOperation.Error}");
                return;
            }

            var versionOperation = package.RequestPackageVersionAsync();
            await versionOperation.ToUniTask();

            if (versionOperation.Status != EOperationStatus.Succeed)
            {
                stopwatch.Stop();
                _loader.HandleLoadResult(null, false, versionOperation.Error);
                Debug.LogError($"[AssetInitializer] Version request failed: {versionOperation.Error}");
                return;
            }

            var manifestOperation = package.UpdatePackageManifestAsync(versionOperation.PackageVersion);
            await manifestOperation.ToUniTask();

            if (manifestOperation.Status != EOperationStatus.Succeed)
            {
                stopwatch.Stop();
                _loader.HandleLoadResult(null, false, manifestOperation.Error);
                Debug.LogError($"[AssetInitializer] Manifest update failed: {manifestOperation.Error}");
                return;
            }

            stopwatch.Stop();
            var adapter = new YooAssetPackageAdapter(package);
            _loader.HandleLoadResult(adapter, true, null);
            Debug.Log($"[AssetInitializer] Completed in {stopwatch.ElapsedMilliseconds}ms.");
        }
    }
}
