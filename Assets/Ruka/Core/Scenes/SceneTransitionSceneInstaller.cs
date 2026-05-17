using Cysharp.Threading.Tasks;
using Ruka.Core.DI;
using Ruka.Core.Resources;
using Ruka.Core.Symbols;
using UnityEngine;
using VContainer;

namespace Ruka.Core.Scenes
{
    [FeatureInstaller(typeof(SceneGroup), order: 45)]
    internal sealed class SceneTransitionSceneInstaller : IFeatureInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            if (!builder.Exists(typeof(SceneTransitionConfig), findParentScopes: false))
                return;

            builder.RegisterBuildCallback(container =>
            {
                var config = container.Resolve<SceneTransitionConfig>();
                if (config.CurtainPrefabKey is not { } key) return;

                var assetScope = container.Resolve<IAssetScope>();
                LoadSceneCurtainAsync(assetScope, key).Forget();
            });
        }

        private static async UniTaskVoid LoadSceneCurtainAsync(IAssetScope assetScope, Symbol<AssetRef> key)
        {
            var go = await assetScope.InstantiateAsync(key);
            Object.DontDestroyOnLoad(go);
            go.GetComponent<SceneTransitionCurtainBase>().MarkEphemeral();
        }
    }
}
