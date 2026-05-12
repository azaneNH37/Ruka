using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;

namespace Ruka.Core.Scenes
{
    public abstract class SceneTransitionCurtainBase : MonoBehaviour, ISceneTransitionCurtain
    {
        protected virtual int PostActivationDelayMs => 0;

        private ICurtainRegistry _registry;
        private bool _isEphemeral;

        internal bool IsEphemeral => _isEphemeral;

        internal void MarkEphemeral() => _isEphemeral = true;

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

        protected virtual void OnProgressUpdated(float progress) { }

        protected virtual UniTask OnLoadCompleteAsync(CancellationToken ct) => UniTask.CompletedTask;

        protected abstract UniTask OnHideAsync(CancellationToken ct);

        UniTask ISceneTransitionCurtain.ShowAsync(CancellationToken ct) => OnShowAsync(ct);

        void ISceneTransitionCurtain.OnProgressUpdated(float progress) => OnProgressUpdated(progress);

        UniTask ISceneTransitionCurtain.OnLoadedAsync(CancellationToken ct) => OnLoadCompleteAsync(ct);

        async UniTask ISceneTransitionCurtain.HideAsync(CancellationToken ct)
        {
            if (PostActivationDelayMs > 0)
                await UniTask.Delay(PostActivationDelayMs, cancellationToken: ct);
            await OnHideAsync(ct);
        }
    }
}
