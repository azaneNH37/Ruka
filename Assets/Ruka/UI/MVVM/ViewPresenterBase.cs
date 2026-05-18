using System;
using System.Collections.Generic;
using R3;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace Ruka.UI.MVVM
{
    /// <summary>
    /// Manages a keyed collection of (View, ViewModel) pairs: instantiates, binds, and destroys them as a unit. Not a replacement for a single IInitializable EntryPoint — use when a presenter must own N instances of the same prefab type, distinguished by TKey.
    /// </summary>
    public abstract class ViewPresenterBase<TKey, TViewModel, TView> : IInitializable, IDisposable
        where TViewModel : class, IViewModel
        where TView : MonoBehaviour, IView<TViewModel>
    {
        private readonly TView _prefab;
        private readonly Transform _parent;
        private readonly IObjectResolver _resolver;

        /// <summary>Active views keyed by TKey. Read from subclasses to access views; use CreateView/RemoveView to modify.</summary>
        protected readonly Dictionary<TKey, TView> Views = new();

        /// <summary>Active ViewModels keyed by TKey. Read from subclasses to access models; use CreateView/RemoveView to modify.</summary>
        protected readonly Dictionary<TKey, TViewModel> Models = new();

        /// <summary>Shared lifetime container. Add R3 subscriptions here; disposed automatically in Dispose().</summary>
        protected readonly CompositeDisposable Disposables = new();

        protected ViewPresenterBase(TView prefab, Transform parent, IObjectResolver resolver)
        {
            _prefab = prefab;
            _parent = parent;
            _resolver = resolver;
        }

        public virtual void Initialize() { }

        /// <summary>Instantiates the prefab, resolves and binds a new ViewModel, and registers them under id.</summary>
        /// <remarks>If id is already registered, the existing view and ViewModel are removed first.</remarks>
        protected void CreateView(TKey id)
        {
            RemoveView(id);
            var (model, view) = CreateInternal();
            Views[id] = view;
            Models[id] = model;
        }

        /// <summary>Instantiates the prefab, resolves, initializes, and binds a new ViewModel with creation-time parameters.</summary>
        /// <remarks>
        /// If id is already registered, the existing pair is removed first.
        /// TViewModel must implement IInitializableViewModel{TParam}; throws InvalidOperationException at runtime otherwise.
        /// </remarks>
        protected void CreateView<TParam>(TKey id, TParam param)
        {
            RemoveView(id);
            var (model, view) = CreateInternal(param);
            Views[id] = view;
            Models[id] = model;
        }

        /// <summary>Destroys the view GameObject and disposes the ViewModel for id. No-op if id is not registered.</summary>
        protected void RemoveView(TKey id)
        {
            if (Views.Remove(id, out var view))
            {
                Object.Destroy(view.gameObject);
                if (Models.Remove(id, out var model))
                    model.Dispose();
            }
        }

        /// <summary>Opts into per-frame OnUpdate() calls via Observable.EveryUpdate(). Call from Initialize() only; the subscription is bound to Disposables.</summary>
        protected void EnableUpdate()
        {
            Observable.EveryUpdate()
                .Subscribe(_ => OnUpdate())
                .AddTo(Disposables);
        }

        /// <summary>Per-frame hook. Override when the presenter needs to push data from logic models into ViewModels each frame.</summary>
        protected virtual void OnUpdate() { }

        public virtual void Dispose()
        {
            Disposables.Dispose();

            foreach (var view in Views.Values)
                Object.Destroy(view.gameObject);
            foreach (var model in Models.Values)
                model.Dispose();

            Views.Clear();
            Models.Clear();
        }

        private (TViewModel model, TView view) CreateInternal()
        {
            var view = _resolver.Instantiate(_prefab, _parent);
            var model = _resolver.Resolve<TViewModel>();
            view.Bind(model);
            return (model, view);
        }

        private (TViewModel model, TView view) CreateInternal<TParam>(TParam param)
        {
            var view = _resolver.Instantiate(_prefab, _parent);
            var model = _resolver.Resolve<TViewModel>();
            if (model is IInitializableViewModel<TParam> initModel)
                initModel.Initialize(param);
            else
                throw new InvalidOperationException(
                    $"{typeof(TViewModel).Name} does not implement IInitializableViewModel<{typeof(TParam).Name}>");
            view.Bind(model);
            return (model, view);
        }
    }
}
