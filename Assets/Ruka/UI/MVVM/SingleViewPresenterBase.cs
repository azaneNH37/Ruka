using R3;
using Ruka.Core.MVVM;
using UnityEngine;
using VContainer;

namespace Ruka.UI.MVVM
{
    /// <summary>
    /// Convenience base for presenters that manage exactly one (View, ViewModel) pair. Wraps ViewPresenterBase with a Unit key so callers never need to supply one.
    /// </summary>
    public abstract class SingleViewPresenterBase<TViewModel, TView>
        : ViewPresenterBase<Unit, TViewModel, TView>
        where TViewModel : class, IViewModel
        where TView : MonoBehaviour, IView<TViewModel>
    {
        protected SingleViewPresenterBase(TView prefab, Transform parent, IObjectResolver resolver)
            : base(prefab, parent, resolver) { }

        /// <summary>Creates the single (View, ViewModel) pair managed by this presenter. If already created, the existing pair is replaced.</summary>
        protected (TView view, TViewModel model) CreateView() => CreateView(Unit.Default);

        /// <summary>Destroys the view and disposes the ViewModel. No-op if not yet created.</summary>
        protected void RemoveView() => RemoveView(Unit.Default);

        /// <summary>The active view, or null if not yet created.</summary>
        protected TView View => Views.TryGetValue(Unit.Default, out var v) ? v : null;

        /// <summary>The active ViewModel, or null if not yet created.</summary>
        protected TViewModel Model => Models.TryGetValue(Unit.Default, out var m) ? m : null;
    }
}
