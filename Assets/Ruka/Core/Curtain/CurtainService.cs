using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;

namespace Ruka.Core.Curtain
{
    internal sealed class CurtainService : ICurtainService, ICurtainRegistry, IDisposable
    {
        private readonly List<ICurtain> _stack = new();
        private readonly ReactiveProperty<bool> _isTransitioning = new(false);

        public ReadOnlyReactiveProperty<bool> IsTransitioning => _isTransitioning;

        void ICurtainRegistry.Push(ICurtain curtain)
        {
            _stack.Add(curtain);
        }

        void ICurtainRegistry.Pop(ICurtain curtain)
        {
            _stack.Remove(curtain);
        }

        public async UniTask TransitionAsync(
            Func<IProgress<float>, CancellationToken, UniTask> work,
            CancellationToken ct = default)
        {
            if (_isTransitioning.Value)
                throw new InvalidOperationException(
                    "A transition is already in progress. Check IsTransitioning before calling TransitionAsync.");

            if (_stack.Count == 0)
                throw new InvalidOperationException(
                    "No curtain registered. Push an ICurtain via ICurtainRegistry before calling TransitionAsync.");

            _isTransitioning.Value = true;

            var curtain = _stack[^1];

            try
            {
                await curtain.ShowAsync(ct);

                var progress = new CurtainProgress(curtain);
                await work(progress, ct);

                await curtain.OnBeforeRevealAsync(ct);

                await curtain.HideAsync(ct);
            }
            finally
            {
                _isTransitioning.Value = false;
            }
        }

        public void Dispose()
        {
            _isTransitioning.Dispose();
        }

        private sealed class CurtainProgress : IProgress<float>
        {
            private readonly ICurtain _curtain;

            public CurtainProgress(ICurtain curtain) => _curtain = curtain;

            public void Report(float value) => _curtain.OnProgressUpdated(value);
        }
    }
}
