using R3;
using Ruka.Core.DI;
using VContainer;

namespace Ruka.Core.Clock
{
    public static class ClockBuilderExtensions
    {
        public static void RegisterClock(
            this IContainerBuilder builder,
            Observable<float> deltaSource,
            TickerConfig config = default)
        {
            builder.RegisterConfig(config);
            builder.Register<LogicTickService>(Lifetime.Singleton)
                .WithParameter(deltaSource)
                .As<ILogicClock>()
                .AsSelf();
            builder.Register<LogicClockController>(Lifetime.Singleton);
        }
    }
}
