using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using ObservableCollections;
using R3;
using Ruka.Core.MVVM;

namespace Ruka.UI.MVVM
{
    /// <summary>
    /// Extends ViewPresenterBase to drive view creation and removal from an IReadOnlyObservableList{TItem}, handling Add/Remove/Replace/Reset deltas automatically.
    /// </summary>
    public abstract class ListPresenterBase<TKey, TItem, TViewModel, TView>
        : ViewPresenterBase<TKey, TViewModel, TView>
        where TViewModel : class, IViewModel
        where TView : UnityEngine.MonoBehaviour, IView<TViewModel>
    {
        protected ListPresenterBase(TView prefab, UnityEngine.Transform parent, VContainer.IObjectResolver resolver)
            : base(prefab, parent, resolver) { }

        /// <summary>Performs an initial full sync with model, then subscribes to incremental deltas for the presenter lifetime.</summary>
        /// <remarks>Call once from Initialize() after the list model is available. The subscription is bound to Disposables.</remarks>
        protected void BindList(IReadOnlyObservableList<TItem> model)
        {
            foreach (var item in model)
                CreateView(GetKey(item));

            model.ObserveChanged()
                .Subscribe(e => ApplyDelta(model, e))
                .AddTo(Disposables);
        }

        /// <summary>Handles a single collection delta event. Move is a no-op by default; override if reordering must be reflected in the view hierarchy.</summary>
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

        /// <summary>Maps a list item to its unique TKey for view registration.</summary>
        protected abstract TKey GetKey(TItem item);
    }
}
