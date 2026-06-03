# Ruka

<div align="center">

[![version](https://img.shields.io/badge/version-0.7.0-blue)](https://github.com/azaneNH37/Ruka/releases) [![unity](https://img.shields.io/badge/Unity-2022.3%2B-black?logo=unity)](https://unity.com/releases/lts) [![license](https://img.shields.io/badge/license-MIT-green)](LICENSE) [![PRs Welcome](https://img.shields.io/badge/PRs-welcome-brightgreen)](https://github.com/azaneNH37/Ruka/pulls)

</div>

面向中等复杂度 Unity 项目的**跨项目稳定框架层**。

---

## 设计公理

以下约束不是风格偏好，是工程权衡的结果。

**无单例** — 单例的本质是隐式全局状态。任何可以被单例解决的问题，都可以被 VContainer 的 Scope 层级更清晰地解决，同时保留生命周期可控性和可测试性。服务通过 `IFeatureInstaller` 注册到对应 Scope，Scope 随业务边界创建和销毁。

**逻辑不写在 MonoBehaviour** — MonoBehaviour 绑定 Unity 生命周期，使业务逻辑无法脱离引擎运行时被测试。Ruka 中所有服务、系统、状态机均为纯 C# 类，通过 VContainer 注入。MonoBehaviour 只作为场景边界层：转发输入、提供注入入口。纯 C# 业务层具备单元测试可行性。

**Scope 生命周期显式化** — 服务的存活周期必须与其服务的业务边界对齐，而不是"挂在 DontDestroyOnLoad 直到进程结束"。VContainer 三层 Scope（Project / Scene / 自定义）精确对齐业务边界：`ProjectScope` 管理整个进程有效的静态数据，`SessionScope` 随网络会话存活，`SceneScope` 随场景销毁。

**类型安全优于字符串 Key** — 资产地址、窗口 ID、存档键等标识符统一使用 `Symbol<T>` 携带类型标记。同一字符串在不同类型的 Symbol 之间不可互换，跨类型 ID 误用在编译期报错，而非运行期崩溃。

**容器只暴露业务服务** — DI 容器的可解析类型应等价于模块的公开业务 API。内部管线数据——驱动源 Observable、状态机规则表、构建器参数——通过 `.WithParameter()` 在构造时注入，不进入容器的公开解析空间。容器一旦可解析任意中间对象，就退化为服务定位器；消费方无法从注册列表推断哪些是真实业务依赖，模块边界随之消解。

以下通用工程原则同样在该框架中严格践行：

**组合优于继承** — 扩展 Scope 能力通过新增 `IFeatureInstaller` 实现，禁止为功能注册创建 `LifetimeScope` 子类；类型语义通过泛型标记（`Symbol<T>` 的 marker struct）而非类型层级表达。

**资源与订阅必须有明确归属** — R3 订阅统一通过 `CompositeDisposable` 或 `CancellationToken` 绑定持有者，禁止裸 Subscribe；加载的资产归属于注入的 `IAssetScope`，随 Scope 销毁自动释放。两者均不允许无所有者地悬挂存在。

---

## 模块

```
Ruka
├── Core.DI ─────────── 所有模块的注册基础设施（VContainer 扩展）
├── Core.Resources ──── YooAsset 封装，引用计数与作用域释放
├── Core.StaticData ─── 静态配置加载，推荐集成Luban
├── Core.Curtain ──────── 视觉过渡遮罩编排
├── Core.Saves ──────── 多槽位存档，ISaveable / ICrossSaveable 接口，SaveKey<T> 类型安全存档键
├── Core.FSM ────────── 纯 C# 有限状态机，FsmRules 声明式转换
├── Core.Registry ───── 类型安全启动期注册，AsyncRegistry 异步等待
├── Core.Clock ──────── 逻辑 Tick 服务，与 Unity 帧更新解耦
├── Core.Random ──────── Xoshiro256** 确定性随机，MasterSeed 多序列
├── Core.Pool ───────── 对象池，IResettable 自动重置
├── Core.Symbols ──────── Symbol<T> 类型安全标识符
│
├── UI.Windows ──────── 窗口系统，全局窗口管理，层级排序，IWindowResult<T> 异步返回值，支持多场景共存
└── UI.MVVM ─────────── MVP/MVVM基础设施，提供ListPresenterBase等常用模板
```

---

## 核心机制

### IFeatureInstaller：功能自动发现与组合

每个模块只需标注 `[FeatureInstaller]` 属性，Editor 工具链自动将其注册到对应 Scope：

```csharp
[FeatureInstaller(typeof(ProjectGroup), order: 30)]
public sealed class ClockInstaller : IFeatureInstaller
{
    public void Install(IContainerBuilder builder)
    {
        builder.RegisterConfig(new TickerConfig());

        var deltaSource = Observable.EveryUpdate().Select(_ => Time.unscaledDeltaTime);
        builder.Register<LogicTickService>(Lifetime.Singleton)
            .WithParameter(deltaSource)
            .As<ILogicClock>()
            .AsSelf();

        builder.Register<LogicClockController>(Lifetime.Singleton);
    }
}
```

每次脚本编译后，`FeatureInstallerManifestProcessor` 通过 `TypeCache.GetTypesWithAttribute<FeatureInstallerAttribute>()` 全量扫描程序集，按 Group 分组、按 Order 排序，将 `AssemblyQualifiedName` 自动写入对应的 `FeatureGroupCollector` 资产。运行时 `GroupedLifetimeScope` 读取 Collector，逐一实例化 Installer 并调用 Install()。

**添加功能 = 新建一个带 `[FeatureInstaller]` 的类，编译后自动生效。** Collector 内容由工具链维护，开发者从不手动编辑它，也不需要修改任何 LifetimeScope 子类。

---

### RegisterConfig：框架配置与消费方覆盖解耦

Installer 声明基准 Config，消费方通过 ScriptableObject 在 Inspector 层覆盖，两端互不感知：

```csharp
// 框架 Installer：声明基准值
builder.RegisterConfig(new TickerConfig()); // Frequency 默认 30 Hz
```

```csharp
// 消费方：定义 Override Asset，继承 FeatureConfigOverride<T>（ScriptableObject）
[CreateAssetMenu(menuName = "Config/TickerConfigOverride")]
public class TickerConfigOverride : FeatureConfigOverride<TickerConfig>
{
    [SerializeField] private int frequency = 60;

    public override TickerConfig Apply(TickerConfig baseline)
        => baseline with { Frequency = frequency }; // record with：只覆盖关心的字段
}
```

将 Override Asset 拖入 `GroupedLifetimeScope` 的 `configOverrides` 列表，`RegisterConfig<T>()` 内部经过 `ConfigOverrideApplier.Apply()` 路由，存在匹配 Override 时以覆盖后的值注册，否则直接使用基准值。Installer 无需任何改动，也无需感知 Override 的存在。

`FeatureConfig` 是 `record` 基类，`with` 表达式天然支持局部字段覆盖而不影响其余字段，不需要为每个配置项单独提供可变 Setter。

---

### Scope 层级：服务与资产的统一生命周期

`IAssetScope` 注册为 `Lifetime.Scoped`，每个 LifetimeScope 持有独立实例，子 Scope 的资产追踪与父 Scope 完全隔离：

```
ProjectScope            ← IAssetLoader（Singleton，全局唯一）
└── BattleScope         ← IAssetScope（Scoped，战斗专属）
    ├── BattleService
    └── UnitSpawnService
```

服务通过构造函数注入 `IAssetScope`，所有加载操作自动归属当前 Scope：

```csharp
public class BattleService : IAsyncStartable, IDisposable
{
    private readonly IAssetScope _assets;
    private readonly ILogicClock _clock;
    private readonly CompositeDisposable _disposables = new();

    public BattleService(IAssetScope assets, ILogicClock clock)
    {
        _assets = assets;
        _clock = clock;
    }

    public async UniTask StartAsync(CancellationToken ct)
    {
        var mapTex = await _assets.LoadAssetAsync<Texture2D>(Assets.MapTexture);
        var unit   = await _assets.InstantiateAsync(Assets.UnitPrefab);
        // unit 上自动附加 AssetReleaseHook：
        // Destroy(unit) → token 立即归还，无需手动调用 Release

        _clock.OnTick.Subscribe(_ => Tick()).AddTo(_disposables);
    }

    public void Dispose() => _disposables.Dispose();
    // _assets 由 VContainer 随 Scope 销毁自动 Dispose，无需在此调用
}
```

战斗结束，销毁 BattleScope 的 GameObject：

```csharp
Destroy(battleScopeGO);
// VContainer 自动调用：
// ① BattleService.Dispose()  → CompositeDisposable 释放，R3 订阅清除
// ② AssetScope.Dispose()     → 剩余 token 批量归还，无内存泄漏
// ProjectScope 及其 IAssetLoader 不受影响
```

`AssetReleaseHook` 和 `Scope.Dispose()` 是两条互补的释放路径：前者处理单个 GameObject 提前销毁时的即时回收，后者在 Scope 结束时兜底清理全部剩余引用。

---

### Symbol&lt;T&gt;：跨类型不可互换的标识符

两个字符串值相同的 Symbol，只要 marker struct 不同，在编译期就无法互换：

```csharp
// 每种语义各自有独立的 marker struct
struct WindowId { }
struct AssetRef { }

Symbol<WindowId> window = new("MainMenu");
Symbol<AssetRef> asset  = new("UI/MainMenu");

// OpenWindowAsync 第一参数是 Symbol<WindowId>，第二参数是 Symbol<AssetRef>
await windowService.OpenWindowAsync<Unit>(window, asset);  // ✓
await windowService.OpenWindowAsync<Unit>(asset, window);  // 编译错误：Symbol<AssetRef> ≠ Symbol<WindowId>

// LoadAssetAsync 只接受 Symbol<AssetRef>
await assetScope.LoadAssetAsync<Sprite>(asset);   // ✓
await assetScope.LoadAssetAsync<Sprite>(window);  // 编译错误：Symbol<WindowId> ≠ Symbol<AssetRef>
```

marker struct 不携带任何运行期数据，泛型参数本身只是编译期标签。`Symbol<WindowId>` 和 `Symbol<AssetRef>` 底层都是一个字符串，但在类型系统中完全隔离 — 这是"类型安全优于字符串 Key"的零运行期开销实现。同理适用于 `SaveKey<T>`：

```csharp
snapshot.TryGet(SaveKeys.Inventory, out InventoryData inv); // ✓
snapshot.TryGet(SaveKeys.Inventory, out int wrong);         // 编译错误：类型不匹配
```

---

### 窗口作为异步函数

打开一个对话框和调用一个异步函数具有完全相同的形式：

```csharp
// 调用方只等返回值，不关心窗口内部如何实现
bool confirmed = await windowService.OpenWindowAsync<bool, ConfirmPayload>(
    Windows.ConfirmDialog,      // Symbol<WindowId>
    Assets.ConfirmDialogPrefab, // Symbol<AssetRef>
    new ConfirmPayload { Message = "确定删除存档吗？" }
);
if (confirmed)
    await saveService.DeleteSlotAsync(activeSlot);
```

```csharp
// 窗口通过 SetResult 交付返回值，之后自动关闭
public class ConfirmDialog : WindowBase<ConfirmPayload, bool>
{
    public void OnConfirmClicked() => SetResult(true);
    public void OnCancelClicked() => SetResult(false);
}
```

`IWindowResult<TResult>` 把 UI 交互拉回"函数调用 / 返回值"模型，消除了事件回调和静态中间变量。调用方通过 `await` 自然地表达"等待用户做决定"的语义，与等待任何其他异步操作没有区别。

---

### ISaveable：集合注入驱动存档，IsMeta 拆分快速预览

服务不主动调用 SaveService，而是实现 `ISaveable` 接口，通过 DI 集合注入自动参与存档流程：

```csharp
// 服务同时注册为自身接口和 ISaveable
builder.Register<InventoryService>(Lifetime.Singleton)
    .As<IInventoryService>()
    .As<ISaveable>();
```

```csharp
public class InventoryService : IInventoryService, ISaveable
{
    private InventoryData _data = new();

    string ISaveable.SaveKey => "inventory"; // 唯一标识此存档块的字符串 key

    bool ISaveable.IsMeta => false; // true = 同时写入 .meta 快速预览文件

    byte[] ISaveable.CaptureState()  => MessagePackSerializer.Serialize(_data);
    void   ISaveable.RestoreState(byte[] data) => _data = MessagePackSerializer.Deserialize<InventoryData>(data);
    void   ISaveable.SetupDefaultState()       => _data = new();
}
```

`SaveService` 构造函数接受 `IEnumerable<ISaveable>`，VContainer 自动将所有注册为 `ISaveable` 的服务收集进来。存档时，SaveService 遍历调用 `CaptureState()` 采集各服务快照；读档时调用 `RestoreState()`。服务本身不持有文件引用，也不依赖 SaveService，双方通过接口完全解耦。

`IsMeta = true` 的服务数据会额外写入独立的 `.meta` 文件（仅此字段，不含完整 `.sav`）。存档选择界面通过 `LoadSnapshotAsync` 只读 `.meta`，以极低开销获取预览数据，无需反序列化整个存档：

```csharp
// 存档选择 UI：只读 .meta，不触碰完整 .sav
var snapshot = await saveService.LoadSnapshotAsync(slot);
if (snapshot != null && snapshot.TryGet(new SaveKey<SlotPreviewData>("slot_preview"), out var preview))
    slotUI.Show(preview.PlayerName, preview.Level, snapshot.Timestamp);
```

---

### 声明式 FSM：转换表与状态逻辑分离

状态类只写 Enter / Exit 副作用，可达关系全部集中声明在注册时：

```csharp
builder.RegisterFsm<GameFsm>(config =>
    config
        .Entry<LobbyState>()
        .On<LobbyState>(GameTriggers.BattleStart).To<LoadingState>()
        .On<LoadingState>(GameTriggers.LoadComplete).To<BattleState>()
        .On<BattleState>(GameTriggers.PlayerDied).To<DefeatState>()
        .On<BattleState>(GameTriggers.AllEnemiesDefeated).To<VictoryState>(() => !player.IsDead)
);
```

这张转换表在注册时编译为字典，`Transit()` 是 O(1) 查找。Guard 函数允许同一触发器根据运行期条件分叉，而不需要在状态类里嵌 if-else。整个状态机的拓扑在代码里一目了然，不散落在各状态的 Update 逻辑中。

---

### Core.Pool 与 UI.MVVM：池化视图的安全接入

`ViewPresenterBase` 为 Pool 预留了两个显式覆盖点：

- `AcquireView()` — 默认 `Instantiate`，覆盖为 `_pool.Get()`
- `ReleaseView()` — 默认 `Destroy`，覆盖为 `_pool.Return()`

与之配套，`ViewBase<T>` 在每次 `Bind()` 前自动 Dispose 旧订阅集合——池化视图被重新分配给另一个 ViewModel 时，不会遗留上一轮的 R3 订阅。二者组合后，MVVM 管线的其余部分（`CreateView` / `RemoveView` / `IInitializableViewModel` 初始化流程）无需任何改动。

以战斗中频繁刷新的敌人血条为例：

```csharp
// ─── ViewModel ─────────────────────────────────────────────────────────────
// IInitializableViewModel<T>：创建时由 Presenter 传入初始数据，而非构造函数注入
public class EnemyHudViewModel : IInitializableViewModel<EnemyHudData>
{
    public ReactiveProperty<float> HpRatio { get; } = new();
    public ReactiveProperty<string> Name { get; } = new();

    public void Initialize(EnemyHudData data)
    {
        HpRatio.Value = (float)data.CurrentHp / data.MaxHp;
        Name.Value = data.DisplayName;
    }

    public void Dispose()
    {
        HpRatio.Dispose();
        Name.Dispose();
    }
}
```

```csharp
// ─── View ───────────────────────────────────────────────────────────────────
// ViewBase<T>：每次 Bind 前自动 Dispose 旧 CompositeDisposable
// 池化视图被重用时不会累积上一轮的 R3 订阅
public class EnemyHudView : ViewBase<EnemyHudViewModel>
{
    [SerializeField] private Image _hpBar;
    [SerializeField] private TMP_Text _nameLabel;

    protected override void OnBind(EnemyHudViewModel vm, CompositeDisposable disposables)
    {
        vm.HpRatio.Subscribe(r => _hpBar.fillAmount = r).AddTo(disposables);
        vm.Name.Subscribe(n => _nameLabel.text = n).AddTo(disposables);
    }
}
```

```csharp
// ─── Presenter ──────────────────────────────────────────────────────────────
// InitializableViewPresenterBase：编译期约束 TViewModel 必须实现 IInitializableViewModel<TParam>
public class EnemyHudPresenter
    : InitializableViewPresenterBase<int, EnemyHudData, EnemyHudViewModel, EnemyHudView>
{
    private readonly IObjectPool<EnemyHudView> _pool;
    private readonly IEnemyService _enemies;

    public EnemyHudPresenter(
        EnemyHudView prefab,
        Transform parent,
        IObjectResolver resolver,
        IObjectPool<EnemyHudView> pool,
        IEnemyService enemies)
        : base(prefab, parent, resolver)
    {
        _pool = pool;
        _enemies = enemies;
    }

    public override void Initialize()
    {
        _enemies.OnSpawned
            .Subscribe(e => CreateView(e.Id, e.HudData))
            .AddTo(Disposables);

        _enemies.OnDespawned
            .Subscribe(id => RemoveView(id))
            .AddTo(Disposables);
    }

    // ↓ 唯一需要改动的两行：把 Instantiate/Destroy 替换为 Get/Return
    protected override EnemyHudView AcquireView() => _pool.Get();
    protected override void ReleaseView(EnemyHudView view) => _pool.Return(view);
    // Return 内部顺序：ViewBase 已在 Bind 时清理订阅 → IResettable.ResetState（如实现）→ SetActive(false) → 入栈
}
```

```csharp
// ─── 注册（场景专属 Scope，属局部专有注册，允许 GroupedLifetimeScope 子类）──
public class BattleScope : GroupedLifetimeScope
{
    [SerializeField] private EnemyHudView _hudPrefab;
    [SerializeField] private Transform _hudPoolRoot;

    protected override void InstallGroups(IContainerBuilder builder)
    {
        base.InstallGroups(builder);  // 运行所有 [FeatureInstaller] 自动发现的 Installer

        // ComponentPool<EnemyHudView> 注册为 IObjectPool<EnemyHudView>
        builder.RegisterPool<EnemyHudView>(
            _hudPrefab, _hudPoolRoot,
            settings: new PoolSettings { InitialSize = 8, MaxInactiveSize = 16 });

        // ViewModel 必须 Transient：每个 CreateView 调用获得一个独立实例
        builder.Register<EnemyHudViewModel>(Lifetime.Transient);
        builder.RegisterEntryPoint<EnemyHudPresenter>()
            .WithParameter<EnemyHudView>(_hudPrefab)
            .WithParameter<Transform>(_hudPoolRoot);
    }
}
```

`InitialSize = 8` 预热 8 个 GameObject 避免战斗开始时的 GC 峰值；`MaxInactiveSize = 16` 在大规模消灭后限制闲置栈膨胀，超出上限的实例在 `Return()` 时直接 `Destroy`。`PoolCapacityExceededException`（仅在设置了 `FixedTotalSize` 时触发）则可用于为血条总数设置硬上限。

---

## 依赖栈

| 库                                                                      | 用途         | 选择理由                                                   |
| ----------------------------------------------------------------------- | ------------ | ---------------------------------------------------------- |
| [VContainer](https://github.com/hadashiA/VContainer)                    | DI 容器      | Unity 生态中性能最优、IL2CPP 完整兼容，原生支持 Scope 层级 |
| [R3](https://github.com/Cysharp/R3)                                     | 响应式编程   | UniRx 现代化替代，与 UniTask 原生互操作                    |
| [UniTask](https://github.com/Cysharp/UniTask)                           | 异步         | Unity 异步标准，IL2CPP 兼容，替代 System.Threading.Task    |
| [YooAsset](https://github.com/tuyoogame/YooAsset)                       | 资产管理     | 生产验证的 AB 管理，支持热更资源分包                       |
| [MessagePipe](https://github.com/Cysharp/MessagePipe)                   | 跨模块消息   | VContainer 原生集成，类型安全发布订阅                      |
| [MessagePack](https://github.com/MessagePack-CSharp/MessagePack-CSharp) | 二进制序列化 | 存档序列化，性能与体积均优于 JSON                          |

---

## 推荐搭配食用

以下库不是 Ruka 的强依赖，但与框架设计高度契合，搭配体验开箱即用。

| 库                                                                   | 用途             | 搭配说明                                                                                                        |
| -------------------------------------------------------------------- | ---------------- | --------------------------------------------------------------------------------------------------------------- |
| [Luban](https://github.com/focus-creative-games/luban)               | 游戏配置生成     | `Core.StaticData` 的官方对接目标；Excel / JSON → 强类型 C# 代码，支持多种输出格式，生产级配置工作流             |
| [TriInspector](https://github.com/codewriter-packages/Tri-Inspector) | Inspector 扩展   | 零侵入属性标注，补全 `[SymbolSelector]` 等自定义 Drawer 的展示层；内置分组、校验、只读等常用特性                |
| [MCP for Unity](https://github.com/CoplayDev/unity-mcp)（Coplay）    | AI 辅助开发      | 将 Claude Code / Cursor 等 AI 助手与 Unity Editor 直连；Ruka 的 `AGENTS.md` 工作流即依赖此桥接                  |
| [Arch](https://github.com/genaray/Arch)                              | 纯 C# ECS        | Archetype + Chunks 高性能 ECS，Unity DOTS 的轻量替代；纯 C# 实现与 Ruka 逻辑层天然共存，可用于高频实体密集系统  |
| [ZString](https://github.com/Cysharp/ZString)                        | 零分配字符串构建 | 在高频 Tick 路径（`Core.Clock`）或日志格式化中替代 `string.Format`，彻底消除字符串拼接产生的 GC                 |
| [ZLogger](https://github.com/Cysharp/ZLogger)                        | 零分配结构化日志 | 基于 ZString 与 C# 10 字符串插值，内置 Unity Debug 日志 Provider，结构化输出零装箱，直接替换 `Debug.Log` 调用链 |

---

## 致谢与参与

感谢所有依赖库的作者——VContainer、R3、UniTask、YooAsset、MessagePipe、MessagePack 的维护者们以严谨的工程态度为 Unity 生态提供了坚实的底座，Ruka 站在这些工作之上。

如果你在使用过程中发现问题、有改进建议，或希望贡献代码，欢迎：

- [提交 Issue](https://github.com/azaneNH37/Ruka/issues) — bug 反馈、设计讨论、功能请求
- [发起 PR](https://github.com/azaneNH37/Ruka/pulls) — 修复、新模块、文档改进均欢迎
