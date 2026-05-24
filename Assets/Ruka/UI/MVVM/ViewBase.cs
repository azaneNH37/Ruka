using R3;
using Ruka.Core.MVVM;
using UnityEngine;

namespace Ruka.UI.MVVM
{
    /// <summary>
    /// Optional MonoBehaviour base for views that require rebind support (e.g. pooled or virtual-list slots). Disposes previous bindings before each Bind call, making repeated Bind safe. Views that are never rebound may implement IView{TViewModel} directly instead.
    /// </summary>
    public abstract class ViewBase<TViewModel> : MonoBehaviour, IView<TViewModel>
        where TViewModel : IViewModel
    {
        private CompositeDisposable _bindings;

        /// <summary>Disposes the previous binding set, then calls OnBind with a fresh CompositeDisposable. Safe to call repeatedly — intended for pooled or virtual-list slots where the same View is rebound to a different ViewModel.</summary>
        public void Bind(TViewModel viewModel)
        {
            _bindings?.Dispose();
            _bindings = new CompositeDisposable();
            OnBind(viewModel, _bindings);
        }

        /// <summary>Establish subscriptions here. All disposables added to <paramref name="disposables"/> are released on the next Bind call or OnDestroy.</summary>
        protected abstract void OnBind(TViewModel viewModel, CompositeDisposable disposables);

        private void OnDestroy() => _bindings?.Dispose();
    }
}
