using System;
using System.Threading;

namespace Ruka.Core.Clock
{
    public sealed class LogicClockController
    {
        private readonly LogicTickService _logicClock;
        private const float NormalTimeScale = 1f;

        private int _pauseCounter;

        internal LogicClockController(LogicTickService logicClock)
        {
            _logicClock = logicClock;
            _logicClock.SetTimeScale(NormalTimeScale);
        }

        public PauseHandle AcquirePauseHandle()
        {
            var pauseCounter = Interlocked.Increment(ref _pauseCounter);
            if (pauseCounter == 1)
                _logicClock.SetPaused(true);

            return new PauseHandle(this);
        }

        private void ReleasePauseHandle()
        {
            var pauseCounter = Interlocked.Decrement(ref _pauseCounter);
            if (pauseCounter > 0)
                return;

            _logicClock.SetPaused(false);
            if (pauseCounter < 0)
                Interlocked.Exchange(ref _pauseCounter, 0);
        }

        public readonly struct PauseHandle : IDisposable
        {
            private readonly LogicClockController _controller;

            internal PauseHandle(LogicClockController controller)
            {
                _controller = controller;
            }

            public void Dispose()
            {
                _controller.ReleasePauseHandle();
            }
        }
    }
}
