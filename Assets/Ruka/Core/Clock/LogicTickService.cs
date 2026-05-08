using System;
using R3;

namespace Ruka.Core.Clock
{
    internal class LogicTickService : ILogicClock, IDisposable
    {
        private readonly ReactiveProperty<long> _currentTick = new(0);
        public ReadOnlyReactiveProperty<long> CurrentTick => _currentTick;

        private readonly Subject<long> _onTickSubject = new();
        public Observable<long> OnTick => _onTickSubject;

        private readonly ReactiveProperty<float> _timeScale = new(1.0f);
        public ReadOnlyReactiveProperty<float> TimeScaleRx => _timeScale;
        internal void SetTimeScale(float scale) => _timeScale.Value = scale;

        private readonly ReactiveProperty<bool> _isPaused = new(false);
        public ReadOnlyReactiveProperty<bool> IsPausedRx => _isPaused;
        internal void SetPaused(bool paused) => _isPaused.Value = paused;

        private readonly float _tickInterval;
        private float _accumulator;

        public float Alpha => _tickInterval > 0f ? _accumulator / _tickInterval : 0f;

        private readonly IDisposable _subscription;

        public LogicTickService(TickerConfig config, Observable<float> deltaSource)
        {
            _tickInterval = 1f / config.Frequency;

            _subscription = deltaSource.Subscribe(delta => OnDelta(delta));
        }

        private void OnDelta(float delta)
        {
            if (_isPaused.CurrentValue) return;

            _accumulator += delta * _timeScale.CurrentValue;
            while (_accumulator >= _tickInterval)
            {
                _currentTick.Value++;
                _onTickSubject.OnNext(_currentTick.Value);
                _accumulator -= _tickInterval;
            }
        }

        public void Dispose()
        {
            _subscription.Dispose();
            _onTickSubject.OnCompleted();
            _onTickSubject.Dispose();
            _timeScale.Dispose();
            _isPaused.Dispose();
            _currentTick.Dispose();
        }
    }
}
