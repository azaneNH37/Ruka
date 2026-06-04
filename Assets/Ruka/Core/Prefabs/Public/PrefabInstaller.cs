using Ruka.Core.DI;
using VContainer;

namespace Ruka.Core.Prefabs
{
    /// <summary>
    /// Registers <see cref="IPrefabFactory"/> as <c>Lifetime.Scoped</c> in ProjectGroup.
    /// </summary>
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
