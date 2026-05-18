> 状态：未实现
> 依赖：`Ruka.Pool` 模块（对象池，尚未实现）；`ViewBase<TViewModel>`（已实现）

# Module: UI.MVVM — VirtualListPresenterBase

## 职责

通过固定数量的 View 槽位（从对象池取用）和全量常驻的 ViewModel，以视口范围索引驱动槽位与 ViewModel 的重绑（rebind），实现大列表 UI 的可见 GameObject 数量与数据规模解耦。

## 非职责

- 不替代 `ListPresenterBase`——两者语义不同：`ListPresenterBase` 以数据事件驱动 View 的存在，`VirtualListPresenterBase` 以视口范围驱动 View 的绑定关系
- 不管理滚动位置计算——视口范围（起始/结束数据索引）由消费方计算后以 `IReadOnlyReactiveProperty` 传入
- 不提供 ViewModel 的池化——ViewModel 按数据条数常驻内存；仅 View（GameObject）从池中取用
- 不处理数据条目的动态增删——全量数据在 `InitializeViewModels` 时固定

## 公开 API（规划）

```csharp
namespace Ruka.UI.MVVM
{
    /// <summary>
    /// Manages N pooled View slots bound to M persistent ViewModels, driven by a viewport index range
    /// rather than data existence. Not a replacement for ListPresenterBase — use when the visible slot
    /// count is fixed and significantly smaller than the total item count.
    /// </summary>
    /// <remarks>TView must inherit ViewBase{TViewModel} to guarantee safe rebind semantics.</remarks>
    public abstract class VirtualListPresenterBase<TItem, TViewModel, TView>
        : IInitializable, IDisposable
        where TViewModel : class, IViewModel
        where TView : ViewBase<TViewModel>
    {
        /// <summary>Shared lifetime container for presenter-level subscriptions.</summary>
        protected CompositeDisposable Disposables { get; }

        protected VirtualListPresenterBase(IObjectPool<TView> pool, IObjectResolver resolver);

        public virtual void Initialize();

        /// <summary>Pre-creates one ViewModel per item via IObjectResolver.Resolve. Call once from Initialize() before BindViewport.</summary>
        /// <remarks>Does not acquire any View slots from the pool.</remarks>
        protected void InitializeViewModels(IReadOnlyList<TItem> items);

        /// <summary>Subscribes to viewportRange; on each change acquires/releases pool slots and rebinds Views to the correct ViewModels.</summary>
        /// <remarks>
        /// Call once from Initialize() after InitializeViewModels.
        /// viewportRange.Value = (startIndex, endIndex) — inclusive, zero-based, clamped to ViewModel count.
        /// </remarks>
        protected void BindViewport(IReadOnlyReactiveProperty<(int start, int end)> viewportRange);

        /// <summary>Applies item data to its ViewModel at construction time. Called once per item in InitializeViewModels.</summary>
        protected abstract void InitializeViewModel(TViewModel viewModel, TItem item);

        public virtual void Dispose();
    }
}
```

## 最小使用示例

```csharp
// 1. ViewModel — 与 ListPresenterBase 场景相同，无特殊要求
public sealed class InventorySlotViewModel : IViewModel
{
    public readonly ReactiveProperty<Sprite> Icon = new();
    public readonly ReactiveProperty<int> Count = new();
    public void Dispose() { Icon.Dispose(); Count.Dispose(); }
}

// 2. View — 必须继承 ViewBase 以支持 rebind
public sealed class InventorySlotView : ViewBase<InventorySlotViewModel>
{
    [SerializeField] private Image _icon;
    [SerializeField] private TMP_Text _count;

    protected override void OnBind(InventorySlotViewModel vm, CompositeDisposable d)
    {
        vm.Icon.Subscribe(s => _icon.sprite = s).AddTo(d);
        vm.Count.Subscribe(n => _count.text = n.ToString()).AddTo(d);
    }
}

// 3. Presenter — 提供初始化实现，消费方注入 viewport 范围
public sealed class InventoryPresenter
    : VirtualListPresenterBase<ItemData, InventorySlotViewModel, InventorySlotView>
{
    private readonly IInventoryService _inventory;
    private readonly IScrollViewport _viewport;

    public InventoryPresenter(
        IObjectPool<InventorySlotView> pool,
        IObjectResolver resolver,
        IInventoryService inventory,
        IScrollViewport viewport)
        : base(pool, resolver)
    {
        _inventory = inventory;
        _viewport = viewport;
    }

    public override void Initialize()
    {
        InitializeViewModels(_inventory.Items);
        BindViewport(_viewport.VisibleRange); // IReadOnlyReactiveProperty<(int start, int end)>
    }

    protected override void InitializeViewModel(InventorySlotViewModel vm, ItemData item)
    {
        vm.Icon.Value = item.Icon;
        vm.Count.Value = item.Count;
    }
}

// 4. 注册 — IObjectPool<InventorySlotView> 由 Ruka.Pool 模块提供
// RegisterMVVM 不适用（构造函数签名不含 prefab/parent）；手动注册
builder.Register<InventorySlotViewModel>(Lifetime.Transient);
builder.RegisterEntryPoint<InventoryPresenter>()
    .WithParameter<IObjectPool<InventorySlotView>>(slotPool);
```

## 关键设计约束

- **`TView : ViewBase<TViewModel>` 是硬约束**：`VirtualListPresenterBase` 在泛型约束中要求 `TView : ViewBase<TViewModel>`，因为槽位复用时 `Bind()` 必须安全地替换旧订阅。实现 `IView<T>` 但未继承 `ViewBase` 的 View 不兼容此基类。
- **ViewModel per item（Option B）**：每个数据条目常驻一个 ViewModel；ViewModel 可持有 per-item 的订阅链，无需在槽位复用时重建。这要求 M 个 ViewModel 在 Presenter 生命周期内全程占用内存，对于 M < 1000 的背包/列表场景可接受。
- **视口范围由消费方提供**：框架只响应 `viewportRange` 变化，不感知滚动位置、元素高度等布局信息。将像素坐标转换为数据索引范围是消费方（ScrollView 适配器）的职责。
- **`InitializeViewModels` 必须在 `BindViewport` 之前调用**：`BindViewport` 订阅立即触发一次计算；若 ViewModel 列表尚未建立，将越界访问。
- **`RegisterMVVM` 不适用**：`VirtualListPresenterBase` 的构造函数接受 `IObjectPool<TView>` 而非 `TView prefab`，不符合 `RegisterMVVM` 的参数约定；需手动注册。

## 依赖

- `Ruka.Pool` — 提供 `IObjectPool<TView>`（Get / Return）
- `VContainer` — `IObjectResolver`（用于 ViewModel 解析）
- `VContainer.Unity` — `IInitializable`
- `R3` — `IReadOnlyReactiveProperty<T>`、`CompositeDisposable`
- `UnityEngine` — `MonoBehaviour`（间接，通过 `ViewBase`）

## 验收条件

- [ ] `BindViewport` 订阅 `viewportRange` 变化；进入视口的索引调用 `pool.Get()` 并 `view.Bind(viewModels[index])`
- [ ] 离开视口的槽位调用 `pool.Return(view)`，View 不被 Destroy
- [ ] `InitializeViewModels` 不调用 `pool.Get()`，不创建任何 View
- [ ] `Dispose` 归还所有活跃槽位到池，释放所有 ViewModel
- [ ] `viewportRange` 超出 ViewModel 列表边界时抛出明确异常，不静默越界
- [ ] 同一帧内 `viewportRange` 连续变化时，槽位状态与最终范围一致（不残留中间状态的激活槽位）
