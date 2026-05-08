using System;
using Cysharp.Threading.Tasks;

namespace Ruka.Core.Resources
{
    public readonly struct SceneResHandle
    {
        private readonly Func<UniTask> _activateFunc;

        internal SceneResHandle(float progress, Func<UniTask> activateFunc)
        {
            Progress = progress;
            _activateFunc = activateFunc;
        }

        public float Progress { get; }

        public UniTask ActivateAsync()
        {
            return _activateFunc?.Invoke() ?? UniTask.CompletedTask;
        }
    }
}
