using Ruka.Core.DI;
using VContainer;
using VContainer.Unity;

namespace Ruka.Core.StaticData
{
    [FeatureInstaller(InstallerGroups.ProjectGroup, order: 100)]
    public sealed class StaticDataInstaller : IFeatureInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            builder.Register<YooAssetStaticDataService>(Lifetime.Singleton)
                .As<IStaticDataService>()
                .AsSelf();
        }
    }
}
