using Ruka.Core.DI;
using VContainer;

namespace Ruka.Core.StaticData
{
    [FeatureInstaller(typeof(ProjectGroup), order: 100)]
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
