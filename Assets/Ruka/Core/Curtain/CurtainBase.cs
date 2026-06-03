using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;

namespace Ruka.Core.Curtain
{
    public abstract class CurtainBase : MonoBehaviour, ICurtain
    {
        protected virtual int PostActivationDelayMs => 0;

        private ICurtainRegistry _registry;

        [Inject]
        private void Construct(ICurtainRegistry registry)
        {
            _registry = registry;
            _registry.Push(this);
        }

        private void OnDestroy()
        {
            _registry?.Pop(this);
        }

        protected abstract UniTask OnShowAsync(CancellationToken ct);

        protected virtual void OnProgress(float progress) { }

        protected virtual UniTask OnBeforeReveal(CancellationToken ct) => UniTask.CompletedTask;

        protected abstract UniTask OnHideAsync(CancellationToken ct);

        UniTask ICurtain.ShowAsync(CancellationToken ct) => OnShowAsync(ct);

        void ICurtain.OnProgressUpdated(float progress) => OnProgress(progress);

        UniTask ICurtain.OnBeforeRevealAsync(CancellationToken ct) => OnBeforeReveal(ct);

        async UniTask ICurtain.HideAsync(CancellationToken ct)
        {
            if (PostActivationDelayMs > 0)
                await UniTask.Delay(PostActivationDelayMs, cancellationToken: ct);
            await OnHideAsync(ct);
        }
    }
}
