# Module: UI.MVVM

## 职责

通过 `ViewPresenterBase` 将 View（MonoBehaviour）和 ViewModel（纯 C# 类）的创建、绑定、销毁封装为以 `TKey` 索引的原子操作；通过 `ListPresenterBase` 接入 `ObservableCollections` 的增量事件驱动列表类 UI 自动同步；通过 `SingleViewPresenterBase`、`InitializableViewPresenterBase` 和 `ViewBase` 提供针对常见场景的编译期安全变体；通过 `VContainerMVVMExtensions` 将完整的 MVVM 注册步骤收拢为单一类型安全的 VContainer 扩展方法。

## 非职责

- 不提供视口驱动的虚拟列表（规划中，见 `ui.mvvm.planned.md`，依赖 Ruka.Pool 模块）
- 不提供具体 UI 组件或业务 ViewModel 实现
- 不提供数据持久化或资源加载

## 公开 API

```csharp
// ── Ruka.Core.MVVM（Ruka.Core 程序集）────────────────────────────────────────
// IViewModel 和 IInitializableViewModel 定义于 Ruka.Core，使业务程序集（Framework、
// Gameplay）可实现它们而无需引用 Ruka.UI。
namespace Ruka.Core.MVVM
{
    /// <summary>
    /// Marker contract for all ViewModels. Lives in Ruka.Core so business assemblies can implement it
    /// without a dependency on Ruka.UI.
    /// ViewModels are plain C# classes that own observable presentation state; Views are the MonoBehaviours that render it.
    /// </summary>
    public interface IViewModel : IDisposable { }

    /// <summary>
    /// Optional extension for ViewModels that require creation-time parameters. Not a replacement for
    /// constructor injection — use when parameters are data-driven and only known at CreateView call time.
    /// </summary>
    public interface IInitializableViewModel<in TParam> : IViewModel
    {
        /// <summary>Applies creation-time parameters to the ViewModel.</summary>
        /// <remarks>Called by ViewPresenterBase before Bind; do not invoke from outside the presenter pipeline.</remarks>
        void Initialize(TParam param);
    }
}

// ── Ruka.UI.MVVM（Ruka.UI 程序集）────────────────────────────────────────────
namespace Ruka.UI.MVVM
{
    /// <summary>MonoBehaviour contract for a view that binds to a typed ViewModel instance.</summary>
    public interface IView<TViewModel> where TViewModel : IViewModel
    {
        /// <summary>Wires up subscriptions between this view and viewModel.</summary>
        /// <remarks>Called by ViewPresenterBase after ViewModel creation and optional initialization; do not call directly.</remarks>
        void Bind(TViewModel viewModel);
    }

    /// <summary>
    /// Optional MonoBehaviour base for views that require rebind support (pooled or virtual-list slots).
    /// Disposes previous bindings before each Bind call, making repeated Bind safe. Not a replacement for
    /// IView{TViewModel} — implement IView directly when the view is never rebound.
    /// </summary>
    public abstract class ViewBase<TViewModel> : MonoBehaviour, IView<TViewModel>
        where TViewModel : IViewModel
    {
        /// <summary>Disposes the previous binding set, then calls OnBind with a fresh CompositeDisposable. Safe to call repeatedly.</summary>
        public void Bind(TViewModel viewModel);

        /// <summary>Establish subscriptions here. All disposables added to <paramref name="disposables"/> are released on the next Bind call or OnDestroy.</summary>
        protected abstract void OnBind(TViewModel viewModel, CompositeDisposable disposables);
    }

    /// <summary>
    /// Manages a keyed collection of (View, ViewModel) pairs: instantiates, binds, and destroys them as a unit.
    /// Not a replacement for a single IInitializable EntryPoint — use when a presenter must own N instances of
    /// the same prefab type, distinguished by TKey.
    /// </summary>
    public abstract class ViewPresenterBase<TKey, TViewModel, TView> : IInitializable, IDisposable
        where TViewModel : class, IViewModel
        where TView : MonoBehaviour, IView<TViewModel>
    {
        /// <summary>Active views keyed by TKey. Read-only; use CreateView/RemoveView to modify.</summary>
        protected IReadOnlyDictionary<TKey, TView> Views { get; }

        /// <summary>Active ViewModels keyed by TKey. Read-only; use CreateView/RemoveView to modify.</summary>
        protected IReadOnlyDictionary<TKey, TViewModel> Models { get; }

        /// <summary>Shared lifetime container. Add R3 subscriptions here; disposed automatically in Dispose().</summary>
        protected CompositeDisposable Disposables { get; }

        protected ViewPresenterBase(TView prefab, Transform parent, IObjectResolver resolver);

        public virtual void Initialize();

        /// <summary>Acquires a View instance. Override to integrate with an object pool instead of Instantiate.</summary>
        protected virtual TView AcquireView();

        /// <summary>Releases a View instance. Override to return it to an object pool instead of Destroy.</summary>
        protected virtual void ReleaseView(TView view);

        /// <summary>Instantiates the prefab, resolves and binds a new ViewModel, and registers them under id.</summary>
        /// <remarks>If id is already registered, the existing pair is removed first.</remarks>
        protected (TView view, TViewModel model) CreateView(TKey id);

        /// <summary>Resolves, initializes with param, and binds a new ViewModel; registers under id.</summary>
        /// <remarks>
        /// If id is already registered, the existing pair is removed first.
        /// TViewModel must implement IInitializableViewModel{TParam}; throws InvalidOperationException otherwise.
        /// Prefer InitializableViewPresenterBase to enforce this constraint at compile time.
        /// </remarks>
        protected (TView view, TViewModel model) CreateView<TParam>(TKey id, TParam param);

        /// <summary>Destroys the view and disposes the ViewModel for id. No-op if id is not registered.</summary>
        protected void RemoveView(TKey id);

        public virtual void Dispose();
    }

    /// <summary>
    /// Convenience base for presenters that manage exactly one (View, ViewModel) pair. Wraps
    /// ViewPresenterBase with a Unit key so callers never need to supply one.
    /// </summary>
    public abstract class SingleViewPresenterBase<TViewModel, TView>
        : ViewPresenterBase<Unit, TViewModel, TView>
        where TViewModel : class, IViewModel
        where TView : MonoBehaviour, IView<TViewModel>
    {
        protected SingleViewPresenterBase(TView prefab, Transform parent, IObjectResolver resolver);

        /// <summary>Creates the single (View, ViewModel) pair managed by this presenter. If already created, the existing pair is replaced.</summary>
        protected (TView view, TViewModel model) CreateView();

        /// <summary>Destroys the view and disposes the ViewModel. No-op if not yet created.</summary>
        protected void RemoveView();

        /// <summary>The active view, or null if not yet created.</summary>
        protected TView View { get; }

        /// <summary>The active ViewModel, or null if not yet created.</summary>
        protected TViewModel Model { get; }
    }

    /// <summary>
    /// ViewPresenterBase variant that enforces at compile time that TViewModel implements
    /// IInitializableViewModel{TParam}. Not a replacement for ViewPresenterBase — use only when every
    /// CreateView call requires a creation-time parameter.
    /// </summary>
    public abstract class InitializableViewPresenterBase<TKey, TParam, TViewModel, TView>
        : ViewPresenterBase<TKey, TViewModel, TView>
        where TViewModel : class, IViewModel, IInitializableViewModel<TParam>
        where TView : MonoBehaviour, IView<TViewModel>
    {
        protected InitializableViewPresenterBase(TView prefab, Transform parent, IObjectResolver resolver);
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

        /// <summary>Handles a single collection delta. Move is a no-op by default; override if reordering must be reflected in the view hierarchy.</summary>
        protected virtual void ApplyDelta(IReadOnlyObservableList<TItem> model, CollectionChangedEvent<TItem> delta);

        /// <summary>Maps a list item to its unique TKey for view registration.</summary>
        protected abstract TKey GetKey(TItem item);
    }

    /// <summary>
    /// IContainerBuilder extensions that register a complete MVVM unit (ViewModel + Presenter + constructor
    /// parameters) as a single compile-time-verified operation. Not a replacement for manual VContainer
    /// registration — skip when TViewModel needs a non-Transient lifetime or is shared across Presenters.
    /// </summary>
    public static class MvvmBuilderExtensions
    {
        /// <summary>Registers TViewModel as Transient and TPresenter as EntryPoint with prefab and parent wired up.</summary>
        public static void RegisterMVVM<TPresenter, TKey, TViewModel, TView>(
            this IContainerBuilder builder, TView prefab, Transform parent)
            where TPresenter : ViewPresenterBase<TKey, TViewModel, TView>
            where TViewModel : class, IViewModel
            where TView : MonoBehaviour, IView<TViewModel>;

        /// <summary>Single-instance variant for SingleViewPresenterBase subclasses.</summary>
        public static void RegisterMVVM<TPresenter, TViewModel, TView>(
            this IContainerBuilder builder, TView prefab, Transform parent)
            where TPresenter : SingleViewPresenterBase<TViewModel, TView>
            where TViewModel : class, IViewModel
            where TView : MonoBehaviour, IView<TViewModel>;
    }
}
```

> 注：
> - `IReadOnlyObservableList<T>` / `ObservableList<T>` 来自 `ObservableCollections` 包
> - `CollectionChangedEvent<T>` / `ObserveChanged()` 来自 `ObservableCollections.R3` 包
> - `NotifyCollectionChangedAction`（`CollectionChangedEvent<T>.Action` 的类型）定义于 BCL `System.Collections.Specialized`
> - `Unit` 来自 `R3` 包

## 最小使用示例

```csharp
// 1. ViewModel — 纯 C#，持有可观察状态；可注入服务
public sealed class PlayerHudViewModel : IViewModel
{
    public readonly ReadOnlyReactiveProperty<string> ScoreText;

    public PlayerHudViewModel(IScoreService score)
    {
        // ViewModel 负责数据变换；View 只订阅结果
        ScoreText = score.Value.Select(v => $"{v:N0}").ToReadOnlyReactiveProperty();
    }

    public void Dispose() => ScoreText.Dispose();
}

// 2a. View — 继承 ViewBase 以获得 rebind 安全（池化场景必须；其他场景推荐）
public sealed class PlayerHudView : ViewBase<PlayerHudViewModel>
{
    [SerializeField] private TMP_Text _scoreLabel;

    protected override void OnBind(PlayerHudViewModel vm, CompositeDisposable d)
        => vm.ScoreText.Subscribe(t => _scoreLabel.text = t).AddTo(d);
}

// 2b. View — 直接实现 IView（非 rebind 场景的最小形态）
public sealed class EnemyView : MonoBehaviour, IView<EnemyViewModel>
{
    [SerializeField] private Slider _healthBar;
    private readonly CompositeDisposable _d = new();

    public void Bind(EnemyViewModel vm)
        => vm.Health.Subscribe(v => _healthBar.value = v).AddTo(_d);

    private void OnDestroy() => _d.Dispose();
}

// 3a. 单实例 Presenter
public sealed class PlayerHudPresenter
    : SingleViewPresenterBase<PlayerHudViewModel, PlayerHudView>
{
    public PlayerHudPresenter(PlayerHudView prefab, Transform parent, IObjectResolver resolver)
        : base(prefab, parent, resolver) { }

    public override void Initialize() => CreateView();
}

// 3b. 列表 Presenter — ObservableList 驱动增量同步
public sealed class EnemyListPresenter
    : ListPresenterBase<int, EnemyData, EnemyViewModel, EnemyView>
{
    private readonly IEnemyService _enemies;

    public EnemyListPresenter(EnemyView prefab, Transform parent, IObjectResolver resolver, IEnemyService enemies)
        : base(prefab, parent, resolver)
    {
        _enemies = enemies;
    }

    public override void Initialize() => BindList(_enemies.Enemies);
    protected override int GetKey(EnemyData item) => item.Id;
}

// 4. 注册 — RegisterMVVM 自动注册 ViewModel 为 Transient，编译期验证类型匹配
public class GameInstaller : IFeatureInstaller
{
    [SerializeField] private PlayerHudView _hudPrefab;
    [SerializeField] private Transform _hudRoot;
    [SerializeField] private EnemyView _enemyPrefab;
    [SerializeField] private Transform _enemyContainer;

    public void Install(IContainerBuilder builder)
    {
        // 单实例（3 个泛型参数）
        builder.RegisterMVVM<PlayerHudPresenter, PlayerHudViewModel, PlayerHudView>(_hudPrefab, _hudRoot);
        // 多实例（4 个泛型参数，TKey 为 int）
        builder.RegisterMVVM<EnemyListPresenter, int, EnemyViewModel, EnemyView>(_enemyPrefab, _enemyContainer);
    }
}
```

## 关键设计约束

- **`Views`/`Models` 只读**：子类只能通过 `CreateView`/`RemoveView` 修改集合；直接操作字典会破坏 View-ViewModel 一致性。
- **`CreateView` 先隐式 `RemoveView`**：对同一 id 连续调用 `CreateView` 等同于替换——旧 GameObject 被 `ReleaseView` 处理，旧 ViewModel 被 Dispose。不要依赖旧实例的存活状态。
- **`AcquireView`/`ReleaseView` 是框架扩展点，不是业务 API**：默认实现分别调用 `Instantiate` 和 `Destroy`；对象池子类覆写这两个方法以接入池。不要在业务逻辑中直接调用。
- **`CreateView<TParam>` 运行时检查**：`ViewPresenterBase` 对 `IInitializableViewModel<TParam>` 的检查在运行时进行。若需编译期保证，继承 `InitializableViewPresenterBase`（`where TViewModel : IInitializableViewModel<TParam>`）。
- **`ListPresenterBase.Move` 为显式 no-op**：`ApplyDelta` 的 `Move` case 不触发任何视图变更；若 UI 需要反映列表顺序，覆写 `ApplyDelta`。
- **`ListPresenterBase.Reset` 不传递创建参数**：Reset 处理路径调用无参 `CreateView`，不支持有参数的 ViewModel 初始化。若 ViewModel 需要 `TParam`，不要依赖 Reset 的默认实现。
- **`RegisterMVVM` 假设 ViewModel 独占于该 Presenter**：方法会将 `TViewModel` 注册为 `Transient`。若同一 ViewModel 类型被多个 Presenter 共用，或需要不同的 Lifetime，则应手动注册 ViewModel 并使用 `builder.RegisterEntryPoint<TPresenter>().WithParameter(...)` 替代。

## 程序集与依赖

### Ruka.Core（IViewModel、IInitializableViewModel 所在程序集）

业务程序集（Framework、Gameplay）只需引用 `Ruka.Core` 即可实现 ViewModel，无需引用 `Ruka.UI`。

### Ruka.UI（其余所有类型所在程序集）

- `Ruka.Core` — `IViewModel`、`IInitializableViewModel`
- `VContainer` — `IObjectResolver`、`IContainerBuilder`、`Instantiate`
- `VContainer.Unity` — `IInitializable`
- `R3` — `CompositeDisposable`、`Unit`
- `ObservableCollections` — `IReadOnlyObservableList<T>`
- `ObservableCollections.R3` — `ObserveChanged()` 扩展方法
- `UnityEngine` — `MonoBehaviour`、`Transform`、`Object.Destroy`
