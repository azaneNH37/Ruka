using Ruka.Core.DI;
using VContainer;

namespace Ruka.Core.Prefabs
{
    [FeatureInstaller(typeof(ProjectGroup), order: 25)]
    public sealed class PrefabInstaller : IFeatureInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            builder.Register<PrefabFactory>(Lifetime.Scoped)
                .As<IPrefabFactory>();
        }
    }
}
