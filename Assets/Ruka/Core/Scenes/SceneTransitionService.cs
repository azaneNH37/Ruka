using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using Ruka.Core.Resources;
using Ruka.Core.Symbols;
using UnityEngine;

namespace Ruka.Core.Scenes
{
    internal sealed class SceneTransitionService : ISceneTransitionService, ICurtainRegistry, IDisposable
    {
        private readonly IAssetLoader _assetLoader;
        private readonly List<ISceneTransitionCurtain> _stack = new();

        private readonly ReactiveProperty<bool> _isTransitioning = new(false);
        private readonly ReactiveProperty<float> _progress = new(0f);

        public ReadOnlyReactiveProperty<bool> IsTransitioning => _isTransitioning;
        public ReadOnlyReactiveProperty<float> Progress => _progress;

        public SceneTransitionService(IAssetLoader assetLoader)
        {
            _assetLoader = assetLoader;
        }

        void ICurtainRegistry.Push(ISceneTransitionCurtain curtain)
        {
            _stack.Add(curtain);
        }

        void ICurtainRegistry.Pop(ISceneTransitionCurtain curtain)
        {
            _stack.Remove(curtain);
        }

        public async UniTask TransitionAsync(Symbol<AssetRef> sceneKey, CancellationToken ct = default)
        {
            if (_isTransitioning.Value)
                throw new InvalidOperationException(
                    "Scene transition is already in progress. Check IsTransitioning before calling TransitionAsync.");

            _isTransitioning.Value = true;
            _progress.Value = 0f;

            ISceneTransitionCurtain curtain = null;

            try
            {
                if (_stack.Count == 0)
                    throw new InvalidOperationException(
                        "No curtain registered. Push an ISceneTransitionCurtain via ICurtainRegistry before calling TransitionAsync.");

                curtain = _stack[_stack.Count - 1];

                await curtain.ShowAsync(ct);

                var handle = await _assetLoader.LoadSceneSingleAsync(sceneKey);

                while (!handle.IsLoaded)
                {
                    ct.ThrowIfCancellationRequested();
                    _progress.Value = handle.Progress;
                    curtain.OnProgressUpdated(handle.Progress);
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }

                _progress.Value = 1f;
                curtain.OnProgressUpdated(1f);
                await curtain.OnLoadedAsync(ct);

                await handle.ActivateAsync();

                await curtain.HideAsync(ct);

                if (curtain is SceneTransitionCurtainBase mb && mb.IsEphemeral)
                    UnityEngine.Object.Destroy(mb.gameObject);
            }
            finally
            {
                _isTransitioning.Value = false;
                _progress.Value = 0f;
            }
        }

        public void Dispose()
        {
            _isTransitioning.Dispose();
            _progress.Dispose();
        }
    }
}
