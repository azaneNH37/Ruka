using Ruka.Core.DI;
using VContainer;
using VContainer.Unity;

namespace Ruka.Core.Resources
{
    [FeatureInstaller(typeof(ProjectGroup), order: 20)]
    public sealed class ResourceInstaller : IFeatureInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            builder.RegisterConfig(new ResourceConfig());
            builder.Register<AssetLoader>(Lifetime.Singleton)
                .As<IAssetLoader>()
                .AsSelf();
            builder.Register<SceneLoadService>(Lifetime.Singleton)
                .As<ISceneLoadService>();
            builder.Register<AssetScope>(Lifetime.Scoped)
                .As<IAssetScope>()
                .AsSelf();
            builder.RegisterEntryPoint<AssetInitializer>();
        }
    }
}
