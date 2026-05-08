using System;
using R3;
using UnityEngine;
using VContainer.Unity;

namespace Ruka.Core.Clock
{
    internal class LogicTickService : ILogicClock, ITickable, IDisposable
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

        public float Alpha => _accumulator / _tickInterval;

        public LogicTickService(TickerConfig config)
        {
            _tickInterval = 1f / config.Frequency;
        }

        public void Tick()
        {
            if (IsPausedRx.CurrentValue) return;
            _accumulator += Time.deltaTime * TimeScaleRx.CurrentValue;
            while (_accumulator >= _tickInterval)
            {
                _currentTick.Value++;
                _onTickSubject.OnNext(_currentTick.Value);
                _accumulator -= _tickInterval;
            }
        }

        public void Dispose()
        {
            _onTickSubject.OnCompleted();
            _onTickSubject.Dispose();
            _timeScale.Dispose();
            _isPaused.Dispose();
            _currentTick.Dispose();
        }
    }
}
