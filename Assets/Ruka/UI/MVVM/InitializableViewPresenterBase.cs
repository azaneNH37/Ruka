using UnityEngine;
using VContainer;

namespace Ruka.UI.MVVM
{
    /// <summary>
    /// ViewPresenterBase variant that enforces at compile time that TViewModel implements IInitializableViewModel{TParam}. Use instead of ViewPresenterBase when every CreateView call requires a creation-time parameter.
    /// </summary>
    public abstract class InitializableViewPresenterBase<TKey, TParam, TViewModel, TView>
        : ViewPresenterBase<TKey, TViewModel, TView>
        where TViewModel : class, IViewModel, IInitializableViewModel<TParam>
        where TView : MonoBehaviour, IView<TViewModel>
    {
        protected InitializableViewPresenterBase(TView prefab, Transform parent, IObjectResolver resolver)
            : base(prefab, parent, resolver) { }
    }
}
