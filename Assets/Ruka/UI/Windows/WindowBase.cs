using Cysharp.Threading.Tasks;
using Ruka.Core.Symbols;
using UnityEngine;

namespace Ruka.UI.Windows
{
    public abstract class WindowBase : MonoBehaviour, IWindowHandle
    {
        [SerializeField] private WindowLayer layer;
        [SerializeField] private bool closeOnBack;

        public Symbol<WindowId> WindowId { get; internal set; }
        public WindowLayer Layer => layer;
        public bool CloseOnBack => closeOnBack;

        public virtual UniTask ShowAsync() => UniTask.CompletedTask;
        public virtual UniTask HideAsync() => UniTask.CompletedTask;

        internal virtual void SetPayload(object payload) { }
    }

    public abstract class WindowBase<TPayload> : WindowBase
    {
        public TPayload Payload { get; internal set; }

        internal override void SetPayload(object payload)
        {
            Payload = (TPayload)payload;
        }
    }

    public abstract class WindowResultBase<TResult> : WindowBase, IWindowResult<TResult>
    {
        private readonly UniTaskCompletionSource<TResult> _resultSource = new();

        protected void SetResult(TResult result) => _resultSource.TrySetResult(result);

        UniTask<TResult> IWindowResult<TResult>.GetResultAsync() => _resultSource.Task;
        void IWindowResultInternal.Cancel() => _resultSource.TrySetCanceled();
    }

    public abstract class WindowBase<TPayload, TResult> : WindowBase<TPayload>, IWindowResult<TResult>
    {
        private readonly UniTaskCompletionSource<TResult> _resultSource = new();

        protected void SetResult(TResult result) => _resultSource.TrySetResult(result);

        UniTask<TResult> IWindowResult<TResult>.GetResultAsync() => _resultSource.Task;
        void IWindowResultInternal.Cancel() => _resultSource.TrySetCanceled();
    }
}
