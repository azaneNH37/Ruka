using System;
using Ruka.Core.DI;
using VContainer;

namespace Ruka.Core.Random
{
    [FeatureInstaller(InstallerGroups.ProjectGroup, order: 25)]
    public sealed class RandomInstaller : IFeatureInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            builder.RegisterInstance(new MasterSeed(Environment.TickCount));

            builder.Register<RandomService>(Lifetime.Singleton)
                .As<IGlobalRandomService>();

            builder.Register<RandomService>(Lifetime.Scoped)
                .As<IRandomService>();
        }
    }
}
