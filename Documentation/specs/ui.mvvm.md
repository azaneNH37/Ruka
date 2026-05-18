# Module: UI.MVVM

## 职责

通过 `ViewPresenterBase` 将 View（MonoBehaviour）和 ViewModel（纯 C# 类）的创建、绑定、销毁封装为以 `TKey` 索引的单一操作单元；通过 `ListPresenterBase` 在此基础上接入 `ObservableCollections` 的增量事件，驱动列表类 UI 的自动同步。

## 非职责

- 不提供 View 对象池（每次 `CreateView` 均实例化新 GameObject）
- 不提供具体 UI 组件或业务 ViewModel 实现
- 不提供数据持久化或资源加载

## 公开 API

```csharp
namespace Ruka.UI.MVVM
{
    /// <summary>
    /// Marker contract for all ViewModels. Not a replacement for MonoBehaviour — ViewModels are plain C# classes
    /// that own observable state; Views are the MonoBehaviours that render it.
    /// </summary>
    public interface IViewModel : IDisposable { }

    /// <summary>MonoBehaviour contract for a view that binds to a typed ViewModel instance.</summary>
    public interface IView<TViewModel> where TViewModel : IViewModel
    {
        /// <summary>Wires up bindings between this view and viewModel.</summary>
        /// <remarks>Called by ViewPresenterBase after ViewModel creation and optional initialization; do not call directly.</remarks>
        void Bind(TViewModel viewModel);
    }

    /// <summary>
    /// Optional extension for ViewModels that require creation-time parameters. Not a replacement for constructor
    /// injection — use when parameters are data-driven and only available at the moment CreateView is called.
    /// </summary>
    public interface IInitializableViewModel<in TParam> : IViewModel
    {
        /// <summary>Applies creation-time parameters to the ViewModel.</summary>
        /// <remarks>Called by ViewPresenterBase before Bind; do not invoke from outside the presenter pipeline.</remarks>
        void Initialize(TParam param);
    }

    /// <summary>
    /// Manages a keyed collection of (View, ViewModel) pairs: instantiates, binds, and destroys them as a unit.
    /// Not a replacement for a single IInitializable EntryPoint — use when a presenter must own N instances of the
    /// same prefab type, distinguished by TKey.
    /// </summary>
    public abstract class ViewPresenterBase<TKey, TViewModel, TView> : IInitializable, IDisposable
        where TViewModel : class, IViewModel
        where TView : MonoBehaviour, IView<TViewModel>
    {
        /// <summary>Active views keyed by TKey. Read from subclasses to access views; use CreateView/RemoveView to modify.</summary>
        protected readonly Dictionary<TKey, TView> Views;

        /// <summary>Active ViewModels keyed by TKey. Read from subclasses to access models; use CreateView/RemoveView to modify.</summary>
        protected readonly Dictionary<TKey, TViewModel> Models;

        /// <summary>Shared lifetime container. Add R3 subscriptions here; disposed automatically in Dispose().</summary>
        protected readonly CompositeDisposable Disposables;

        protected ViewPresenterBase(TView prefab, Transform parent, IObjectResolver resolver);

        public virtual void Initialize();

        /// <summary>Instantiates the prefab, resolves and binds a new ViewModel, and registers them under id.</summary>
        /// <remarks>If id is already registered, the existing view and ViewModel are removed first.</remarks>
        protected void CreateView(TKey id);

        /// <summary>Instantiates the prefab, resolves, initializes, and binds a new ViewModel with creation-time parameters.</summary>
        /// <remarks>
        /// If id is already registered, the existing pair is removed first.
        /// TViewModel must implement IInitializableViewModel{TParam}; throws InvalidOperationException at runtime otherwise.
        /// </remarks>
        protected void CreateView<TParam>(TKey id, TParam param);

        /// <summary>Destroys the view GameObject and disposes the ViewModel for id. No-op if id is not registered.</summary>
        protected void RemoveView(TKey id);

        /// <summary>Opts into per-frame OnUpdate() calls via Observable.EveryUpdate(). Call from Initialize() only.</summary>
        protected void EnableUpdate();

        /// <summary>Per-frame hook. Override when the presenter needs to push data from logic models into ViewModels each frame.</summary>
        protected virtual void OnUpdate();

        public virtual void Dispose();
    }

    /// <summary>
    /// Extends ViewPresenterBase to drive view creation and removal from an IReadOnlyObservableList{TItem},
    /// handling Add/Remove/Replace/Reset deltas automatically.
    /// </summary>
    public abstract class ListPresenterBase<TKey, TItem, TViewModel, TView>
        : ViewPresenterBase<TKey, TViewModel, TView>
        where TViewModel : class, IViewModel
        where TView : MonoBehaviour, IView<TViewModel>
    {
        protected ListPresenterBase(TView prefab, Transform parent, IObjectResolver resolver);

        /// <summary>Performs an initial full sync with model, then subscribes to incremental deltas for the presenter lifetime.</summary>
        /// <remarks>Call once from Initialize() after the list model is available. The subscription is bound to Disposables.</remarks>
        protected void BindList(IReadOnlyObservableList<TItem> model);

        /// <summary>Handles a single collection delta event. Move is a no-op by default; override if reordering must be reflected in the view hierarchy.</summary>
        protected virtual void ApplyDelta(IReadOnlyObservableList<TItem> model, CollectionChangedEvent<TItem> delta);

        /// <summary>Maps a list item to its unique TKey for view registration.</summary>
        protected abstract TKey GetKey(TItem item);
    }
}
```

> 注：
> - `IReadOnlyObservableList<T>` / `ObservableList<T>` 来自 `ObservableCollections` 包
> - `CollectionChangedEvent<T>` / `ObserveChanged()` 扩展方法来自 `ObservableCollections.R3` 包
> - `NotifyCollectionChangedAction`（`CollectionChangedEvent<T>.Action` 的类型）定义于 BCL `System.Collections.Specialized`

## 最小使用示例

```csharp
// 1. Marker struct and ViewModel (plain C#, owns reactive state)
public struct EnemyId { }

public sealed class EnemyViewModel : IViewModel
{
    public readonly ReactiveProperty<float> Health = new();
    public void Dispose() => Health.Dispose();
}

// 2. View — MonoBehaviour, binds to ViewModel; manages its own subscription lifetime
public sealed class EnemyView : MonoBehaviour, IView<EnemyViewModel>
{
    [SerializeField] private Slider _healthBar;
    private readonly CompositeDisposable _d = new();

    public void Bind(EnemyViewModel vm)
        => vm.Health.Subscribe(v => _healthBar.value = v).AddTo(_d);

    private void OnDestroy() => _d.Dispose();
}

// 3a. Simple presenter — reacts to spawn/despawn events, pushes frame data via OnUpdate
public sealed class EnemyPresenter : ViewPresenterBase<int, EnemyViewModel, EnemyView>
{
    private readonly IEnemyModel _model;

    public EnemyPresenter(EnemyView prefab, Transform parent, IObjectResolver resolver, IEnemyModel model)
        : base(prefab, parent, resolver)
    {
        _model = model;
    }

    public override void Initialize()
    {
        _model.OnSpawned.Subscribe(id => CreateView(id)).AddTo(Disposables);
        _model.OnDespawned.Subscribe(id => RemoveView(id)).AddTo(Disposables);
        EnableUpdate();  // opt into OnUpdate
    }

    protected override void OnUpdate()
    {
        foreach (var (id, vm) in Models)
            vm.Health.Value = _model.GetHealth(id);
    }
}

// 3b. List presenter — driven by ObservableList, no manual subscribe needed
public sealed class EnemyListPresenter : ListPresenterBase<int, EnemyData, EnemyViewModel, EnemyView>
{
    public EnemyListPresenter(EnemyView prefab, Transform parent, IObjectResolver resolver, IEnemyStore store)
        : base(prefab, parent, resolver)
        => _list = store.Enemies;  // ObservableList<EnemyData>

    private readonly ObservableList<EnemyData> _list;

    protected override int GetKey(EnemyData item) => item.Id;

    public override void Initialize() => BindList(_list);
}

// 4. Register — prefab and parent are not DI types; pass via WithParameter
builder.RegisterEntryPoint<EnemyPresenter>()
    .WithParameter(enemyViewPrefab)   // resolved as TView (EnemyView)
    .WithParameter(enemyViewParent);  // resolved as Transform
// IObjectResolver is injected automatically by VContainer
```

## 关键设计约束

- **`CreateView<TParam>` 要求运行时实现 `IInitializableViewModel<TParam>`**: 编译器不强制检查，调用时若 ViewModel 未实现对应接口则抛 `InvalidOperationException`。若需在编译期捕获，需在 Presenter 的泛型约束上加 `where TViewModel : IInitializableViewModel<TParam>`。
- **`CreateView` 先隐式 RemoveView**: 对同一 id 连续调用 `CreateView` 等同于替换——旧的 GameObject 被销毁、ViewModel 被 Dispose。不要依赖旧实例的存活状态。
- **`EnableUpdate` 必须在 `Initialize()` 中调用**: 订阅被加入 `Disposables`；若在 `Dispose()` 之后调用，订阅将泄漏。
- **prefab 和 parent 通过 `.WithParameter()` 传入**: 这两者不是容器中的注册类型，不能通过构造函数自动注入，必须在注册时显式传参。
- **`ListPresenterBase.Move` 为显式 no-op**: `ApplyDelta` 的 `Move` case 不触发任何视图变更。若 UI 需要反映列表顺序，需覆写 `ApplyDelta`。
- **ViewModel 应注册为 `Transient`**: 每次 `CreateView` 都通过 `IObjectResolver.Resolve<TViewModel>()` 解析一个新实例，`Singleton` 会导致多个 View 共享同一个 ViewModel 实例。

## 依赖

- `Ruka.Core`（`VContainer.Unity.IInitializable`）
- `VContainer` — `IObjectResolver`，`Instantiate`
- `R3` — `Observable.EveryUpdate()`，`CompositeDisposable`
- `ObservableCollections` — `IReadOnlyObservableList<T>`
- `ObservableCollections.R3` — `ObserveChanged()` 扩展方法
- `UnityEngine` — `MonoBehaviour`，`Transform`，`Object.Destroy`
