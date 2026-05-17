using R3;
using Ruka.Core.DI;
using UnityEngine;
using VContainer;

namespace Ruka.Core.Clock
{
    [FeatureInstaller(typeof(SceneGroup), order: 30)]
    public sealed class ClockInstaller : IFeatureInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            builder.RegisterConfig(new TickerConfig());

            var deltaSource = Observable.EveryUpdate().Select(_ => Time.unscaledDeltaTime);

            builder.Register<LogicTickService>(Lifetime.Singleton)
                .WithParameter(deltaSource)
                .As<ILogicClock>()
                .AsSelf();

            builder.Register<LogicClockController>(Lifetime.Singleton);
        }
    }
}
