using Ruka.Core.DI;
using VContainer;
using VContainer.Unity;

namespace Ruka.Core.Saves
{
    [FeatureInstaller(typeof(ProjectGroup), order: 60)]
    internal sealed class SavesInstaller : IFeatureInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            builder.RegisterConfig(new SavesConfig());

            builder.Register<LocalFileStorage>(Lifetime.Singleton)
                   .As<ISaveStorage>();

            builder.Register<MigrationRunner<SlotChannel>>(Lifetime.Singleton);

            builder.RegisterEntryPoint<SaveService>()
                   .As<ISaveService>();
        }
    }
}
