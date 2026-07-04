using System;
using System.Collections.Generic;
using R3;
using Ruka.Core.MVVM;
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

        private readonly Dictionary<TKey, TView> _views = new();
        private readonly Dictionary<TKey, TViewModel> _models = new();

        /// <summary>Active views keyed by TKey. Read-only; use CreateView/RemoveView to modify.</summary>
        protected IReadOnlyDictionary<TKey, TView> Views => _views;

        /// <summary>Active ViewModels keyed by TKey. Read-only; use CreateView/RemoveView to modify.</summary>
        protected IReadOnlyDictionary<TKey, TViewModel> Models => _models;

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
        protected (TView view, TViewModel model) CreateView(TKey id)
        {
            RemoveView(id);
            var pair = CreateInternal(AcquireView());
            _views[id] = pair.view;
            _models[id] = pair.model;
            return pair;
        }

        /// <summary>Instantiates the prefab under a specific parent, resolves and binds a new ViewModel.</summary>
        /// <remarks>If id is already registered, the existing pair is removed first.</remarks>
        protected (TView view, TViewModel model) CreateView(TKey id, Transform parent)
        {
            RemoveView(id);
            var pair = CreateInternal(AcquireView(parent));
            _views[id] = pair.view;
            _models[id] = pair.model;
            return pair;
        }

        /// <summary>Instantiates the prefab, resolves, initializes, and binds a new ViewModel with creation-time parameters.</summary>
        /// <remarks>
        /// If id is already registered, the existing pair is removed first.
        /// TViewModel must implement IInitializableViewModel{TParam}; throws InvalidOperationException at runtime otherwise.
        /// Prefer InitializableViewPresenterBase to enforce this constraint at compile time.
        /// </remarks>
        protected (TView view, TViewModel model) CreateView<TParam>(TKey id, TParam param)
        {
            RemoveView(id);
            var pair = CreateInternal(AcquireView(), param);
            _views[id] = pair.view;
            _models[id] = pair.model;
            return pair;
        }

        /// <summary>Instantiates under a specific parent, resolves, initializes with param, and binds.</summary>
        /// <remarks>If id is already registered, the existing pair is removed first.</remarks>
        protected (TView view, TViewModel model) CreateView<TParam>(TKey id, TParam param, Transform parent)
        {
            RemoveView(id);
            var pair = CreateInternal(AcquireView(parent), param);
            _views[id] = pair.view;
            _models[id] = pair.model;
            return pair;
        }

        /// <summary>Destroys the view GameObject and disposes the ViewModel for id. No-op if id is not registered.</summary>
        protected void RemoveView(TKey id)
        {
            if (_views.Remove(id, out var view))
            {
                ReleaseView(view);
                if (_models.Remove(id, out var model))
                    model.Dispose();
            }
        }

        /// <summary>Acquires a View instance under the default parent. Override to integrate with an object pool.</summary>
        protected virtual TView AcquireView() => AcquireView(_parent);

        /// <summary>Acquires a View instance under a specific parent. Override to integrate with an object pool.</summary>
        protected virtual TView AcquireView(Transform parent) => _resolver.Instantiate(_prefab, parent);

        /// <summary>Releases a View instance. Override to return it to an object pool instead of Destroy.</summary>
        protected virtual void ReleaseView(TView view) => Object.Destroy(view.gameObject);

        public virtual void Dispose()
        {
            Disposables.Dispose();

            foreach (var view in _views.Values)
                ReleaseView(view);
            foreach (var model in _models.Values)
                model.Dispose();

            _views.Clear();
            _models.Clear();
        }

        private (TView view, TViewModel model) CreateInternal(TView view)
        {
            var model = _resolver.Resolve<TViewModel>();
            view.Bind(model);
            return (view, model);
        }

        private (TView view, TViewModel model) CreateInternal<TParam>(TView view, TParam param)
        {
            var model = _resolver.Resolve<TViewModel>();
            if (model is IInitializableViewModel<TParam> initModel)
                initModel.Initialize(param);
            else
                throw new InvalidOperationException(
                    $"{typeof(TViewModel).Name} does not implement IInitializableViewModel<{typeof(TParam).Name}>");
            view.Bind(model);
            return (view, model);
        }
    }
}
