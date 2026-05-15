using Ruka.Core.DI;
using VContainer;
using VContainer.Unity;

namespace Ruka.Core.Saves
{
    [FeatureInstaller(InstallerGroups.ProjectGroup, order: 61)]
    internal sealed class CrossSaveInstaller : IFeatureInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            builder.RegisterConfig(new CrossSaveConfig());

            builder.Register<MigrationRunner<CrossChannel>>(Lifetime.Singleton);

            builder.RegisterEntryPoint<CrossSaveService>()
                   .As<ICrossSaveService>();
        }
    }
}
