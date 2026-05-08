using R3;
using Ruka.Core.DI;
using UnityEngine;
using VContainer;

namespace Ruka.Core.Clock
{
    [FeatureInstaller(InstallerGroups.ProjectGroup, order: 30)]
    public sealed class ClockInstaller : IFeatureInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            builder.RegisterConfig(new TickerConfig());

            builder.RegisterInstance<Observable<float>>(
                Observable.EveryUpdate().Select(_ => Time.unscaledDeltaTime));

            builder.Register<LogicTickService>(Lifetime.Singleton)
                .As<ILogicClock>()
                .AsSelf();

            builder.Register<LogicClockController>(Lifetime.Singleton);
        }
    }
}
