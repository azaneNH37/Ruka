using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using ObservableCollections;
using R3;

namespace Ruka.UI.MVVM
{
    public abstract class ListPresenterBase<TKey, TItem, TViewModel, TView>
        : ViewPresenterBase<TKey, TViewModel, TView>
        where TViewModel : class, IViewModel
        where TView : UnityEngine.MonoBehaviour, IView<TViewModel>
    {
        protected ListPresenterBase(TView prefab, UnityEngine.Transform parent, VContainer.IObjectResolver resolver)
            : base(prefab, parent, resolver) { }

        protected void BindList(IReadOnlyObservableList<TItem> model)
        {
            foreach (var item in model)
                CreateView(GetKey(item));

            model.ObserveChanged()
                .Subscribe(e => ApplyDelta(model, e))
                .AddTo(Disposables);
        }

        protected virtual void ApplyDelta(IReadOnlyObservableList<TItem> model, CollectionChangedEvent<TItem> delta)
        {
            switch (delta.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    CreateView(GetKey(delta.NewItem));
                    break;

                case NotifyCollectionChangedAction.Remove:
                    RemoveView(GetKey(delta.OldItem));
                    break;

                case NotifyCollectionChangedAction.Replace:
                    RemoveView(GetKey(delta.OldItem));
                    CreateView(GetKey(delta.NewItem));
                    break;

                case NotifyCollectionChangedAction.Move:
                    break;

                case NotifyCollectionChangedAction.Reset:
                    var keys = Views.Keys.ToArray();
                    foreach (var key in keys)
                        RemoveView(key);
                    foreach (var item in model)
                        CreateView(GetKey(item));
                    break;
            }
        }

        protected abstract TKey GetKey(TItem item);
    }
}
