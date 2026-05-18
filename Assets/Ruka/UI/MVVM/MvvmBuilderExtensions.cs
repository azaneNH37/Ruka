using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Ruka.UI.MVVM
{
    /// <summary>
    /// IContainerBuilder extensions that register a complete MVVM unit (ViewModel + Presenter + constructor parameters)
    /// as a single compile-time-verified operation. Not a replacement for manual VContainer registration — skip these
    /// when TViewModel needs a non-Transient lifetime or is shared across multiple Presenters.
    /// </summary>
    public static class MvvmBuilderExtensions
    {
        /// <summary>
        /// Registers a complete MVVM unit: TViewModel as Transient and TPresenter as an EntryPoint with prefab and parent wired up. The generic constraints guarantee at compile time that TPresenter, TViewModel, and TView are mutually compatible.
        /// </summary>
        public static void RegisterMVVM<TPresenter, TKey, TViewModel, TView>(
            this IContainerBuilder builder,
            TView prefab,
            Transform parent)
            where TPresenter : ViewPresenterBase<TKey, TViewModel, TView>
            where TViewModel : class, IViewModel
            where TView : MonoBehaviour, IView<TViewModel>
        {
            builder.Register<TViewModel>(Lifetime.Transient);
            builder.RegisterEntryPoint<TPresenter>()
                .WithParameter<TView>(prefab)
                .WithParameter<Transform>(parent);
        }

        /// <summary>
        /// Single-instance variant. Registers TViewModel as Transient and TPresenter (a SingleViewPresenterBase) as an EntryPoint with prefab and parent wired up.
        /// </summary>
        public static void RegisterMVVM<TPresenter, TViewModel, TView>(
            this IContainerBuilder builder,
            TView prefab,
            Transform parent)
            where TPresenter : SingleViewPresenterBase<TViewModel, TView>
            where TViewModel : class, IViewModel
            where TView : MonoBehaviour, IView<TViewModel>
        {
            builder.Register<TViewModel>(Lifetime.Transient);
            builder.RegisterEntryPoint<TPresenter>()
                .WithParameter<TView>(prefab)
                .WithParameter<Transform>(parent);
        }
    }
}
