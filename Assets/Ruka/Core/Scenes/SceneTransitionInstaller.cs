using Cysharp.Threading.Tasks;
using Ruka.Core.DI;
using Ruka.Core.Resources;
using Ruka.Core.Symbols;
using UnityEngine;
using VContainer;

namespace Ruka.Core.Scenes
{
    [FeatureInstaller(typeof(ProjectGroup), order: 45)]
    internal sealed class SceneTransitionInstaller : IFeatureInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            builder.RegisterConfig(new SceneTransitionConfig());
            builder.Register<SceneTransitionService>(Lifetime.Singleton)
                .As<ISceneTransitionService>()
                .As<ICurtainRegistry>();

            builder.RegisterBuildCallback(container =>
            {
                var registry = container.Resolve<ICurtainRegistry>();
                registry.Push(new DefaultSceneCurtain());

                var config = container.Resolve<SceneTransitionConfig>();
                if (config.CurtainPrefabKey is { } key)
                {
                    var assetScope = container.Resolve<IAssetScope>();
                    LoadProjectCurtainAsync(assetScope, key).Forget();
                }
            });
        }

        private static async UniTaskVoid LoadProjectCurtainAsync(IAssetScope assetScope, Symbol<AssetRef> key)
        {
            var go = await assetScope.InstantiateAsync(key);
            Object.DontDestroyOnLoad(go);
        }
    }
}
