using System;
using Cysharp.Threading.Tasks;

namespace Ruka.Core.Resources
{
    public readonly struct SceneResHandle
    {
        private readonly Func<float> _progressFunc;
        private readonly Func<bool> _isLoadedFunc;
        private readonly Func<UniTask> _activateFunc;

        internal SceneResHandle(Func<float> progressFunc, Func<bool> isLoadedFunc, Func<UniTask> activateFunc)
        {
            _progressFunc = progressFunc;
            _isLoadedFunc = isLoadedFunc;
            _activateFunc = activateFunc;
        }

        public float Progress => _progressFunc?.Invoke() ?? 0f;
        public bool IsLoaded => _isLoadedFunc?.Invoke() ?? true;

        public async UniTask ActivateAsync()
        {
            if (_activateFunc != null)
                await _activateFunc();
        }
    }
}
