using R3;

namespace Ruka.Core.Clock
{
    public interface ILogicClock
    {
        ReadOnlyReactiveProperty<long> CurrentTick { get; }
        Observable<long> OnTick { get; }
        ReadOnlyReactiveProperty<float> TimeScaleRx { get; }
        ReadOnlyReactiveProperty<bool> IsPausedRx { get; }
        float TickInterval { get; }
        float Alpha { get; }
    }
}
