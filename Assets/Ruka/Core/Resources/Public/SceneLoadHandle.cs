using System;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;

namespace Ruka.Core.Resources
{
    public sealed class SceneLoadHandle
    {
        private readonly Func<float> _progressFunc;
        private readonly Func<bool> _isDoneFunc;
        private readonly Func<Scene> _sceneObjectFunc;
        private readonly Action _activateAction;
        private readonly Func<UniTask> _unloadFunc;

        internal SceneLoadHandle(
            Func<float> progressFunc,
            Func<bool> isDoneFunc,
            Func<Scene> sceneObjectFunc,
            Action activateAction,
            Func<UniTask> unloadFunc)
        {
            _progressFunc = progressFunc;
            _isDoneFunc = isDoneFunc;
            _sceneObjectFunc = sceneObjectFunc;
            _activateAction = activateAction;
            _unloadFunc = unloadFunc;
        }

        public float Progress => _progressFunc();

        public bool IsDone => _isDoneFunc();

        public Scene SceneObject => _sceneObjectFunc();

        public void Activate() => _activateAction();

        public UniTask UnloadAsync() => _unloadFunc();
    }
}
