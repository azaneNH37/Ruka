using Ruka.Core.DI;
using VContainer;
using VContainer.Unity;

namespace Ruka.Core.Clock
{
    [FeatureInstaller(InstallerGroups.ProjectGroup, order: 30)]
    public sealed class ClockInstaller : IFeatureInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            builder.RegisterConfig(new TickerConfig());
            builder.RegisterEntryPoint<LogicTickService>()
                .As<ILogicClock>()
                .AsSelf();
            builder.Register<LogicClockController>(Lifetime.Singleton);
        }
    }
}
