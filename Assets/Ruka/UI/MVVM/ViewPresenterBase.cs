using System;
using System.Collections.Generic;
using R3;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace Ruka.UI.MVVM
{
    public abstract class ViewPresenterBase<TKey, TViewModel, TView> : IInitializable, IDisposable
        where TViewModel : class, IViewModel
        where TView : MonoBehaviour, IView<TViewModel>
    {
        private readonly TView _prefab;
        private readonly Transform _parent;
        private readonly IObjectResolver _resolver;

        protected readonly Dictionary<TKey, TView> Views = new();
        protected readonly Dictionary<TKey, TViewModel> Models = new();
        protected readonly CompositeDisposable Disposables = new();

        protected ViewPresenterBase(TView prefab, Transform parent, IObjectResolver resolver)
        {
            _prefab = prefab;
            _parent = parent;
            _resolver = resolver;
        }

        public virtual void Initialize() { }

        protected void CreateView(TKey id)
        {
            RemoveView(id);
            var (model, view) = CreateInternal();
            Views[id] = view;
            Models[id] = model;
        }

        protected void CreateView<TParam>(TKey id, TParam param)
        {
            RemoveView(id);
            var (model, view) = CreateInternal(param);
            Views[id] = view;
            Models[id] = model;
        }

        protected void RemoveView(TKey id)
        {
            if (Views.Remove(id, out var view))
            {
                Object.Destroy(view.gameObject);
                if (Models.Remove(id, out var model))
                    model.Dispose();
            }
        }

        protected void EnableUpdate()
        {
            Observable.EveryUpdate()
                .Subscribe(_ => OnUpdate())
                .AddTo(Disposables);
        }

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
