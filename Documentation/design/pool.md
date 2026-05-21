# Pool — 设计哲学

## 设计立场

### Unity ObjectPool 的做法

`UnityEngine.Pool.ObjectPool<T>`（Unity 2021+）是纯委托驱动的实现：构造时注入四个回调（`createFunc`、`actionOnGet`、`actionOnRelease`、`actionOnDestroy`），池本身对"对象是什么"一无所知。`IObjectPool<T>` 接口提供统一的 `Get()`/`Release()` 入口，`CollectionCheck` 在开发期检测重复归还。对象完全不感知池的存在，所有生命周期逻辑集中在创建池的那一方。

### Zenject MemoryPool 的做法

Zenject 以抽象基类替代委托：消费方继承 `MemoryPool<Foo>` 并覆写 `OnCreated`、`OnSpawned`、`OnDespawned`、`OnDestroyed` 虚方法，约定将 `Pool` 类作为对象的嵌套类（从而访问私有字段）。进一步提供 `IPoolable<IMemoryPool>` 接口，让对象持有池引用并通过 `IDisposable.Dispose()` 实现自归还。`MonoMemoryPool<T>` 内置 `SetActive` 处理，`ExpandByDoubling` 策略支持翻倍扩容，`BindMemoryPool` 将池深度整合进 DiContainer。

### 本模块的立场

从两者各取一部分，同时明确拒绝另一部分。

**取自 Unity**：委托作为主要扩展机制（`Func<T> create`、`Action<T> onReturn`、`Action<T> onDestroy`），`CollectionCheck` 双归还检测，`IObjectPool<T>` 接口统一池类型。

**取自 Zenject**：`ComponentPool<T>` 内置 `SetActive`（等价于 `MonoMemoryPool`），`FixedTotalSize` 硬上限语义（等价于 `WithFixedSize`），`InitialSize` 预热，`Clear()`/`Dispose()` 显式管理。

**明确背离**：

- **拒绝嵌套 Pool 类约定**：Zenject 约定将 `Pool : MemoryPool<Foo>` 写在 `Foo` 内部，使对象与池绑定。Ruka 的 MVVM 层明确要求 View 不感知自己被池化——Presenter 管理 View 生命周期，View 不应知道自己从哪里来。

- **拒绝 `IPoolable` 的池引用耦合**：`IPoolable<IMemoryPool>` 让对象持有 `IMemoryPool _pool` 字段、实现 `OnSpawned`/`OnDespawned`、通过 `Dispose()` 自归还。这把"我被谁管理"的知识注入了对象，与 DI 框架"依赖倒置"的方向相反——对象依赖了基础设施而非业务接口。在非池化上下文（测试、单独创建）中此类对象会因 `_pool == null` 而行为异常。

- **以 `IResettable` 替代**：对象只需声明"我能清理自己的内生状态"（`void ResetState()`），不需要知道谁调用它、为什么调用。池在 `Return()` 时自动检测并调用，不要求任何显式注册。

- **拒绝 Dispose 自归还模式**：`foo.Dispose()` = `pool.Return(foo)` 的模式将生命周期控制权从 Presenter/所有者转移给对象自身。在 Ruka 的架构中，Presenter 是管理 View 生命周期的正确位置；对象决定自己的"死亡时机"是职责错位。

- **拒绝 `ExpandByDoubling`**：翻倍扩容对每帧数十次 Spawn 的高频场景有价值，但 Ruka 面向的典型池规模（10–200）从中获益微乎其微；引入该策略的复杂度和内存峰值风险不值得承担。

---

## 核心取舍

| 特性 | 说明 |
|---|---|
| `ComponentPool<T>` 内置 SetActive | 所有 MonoBehaviour 池共享的平台约定不再是每个池都要重写的样板；`onReturn`/`ResetState()` 先于 `SetActive(false)` 的顺序保证 OnDisable 触发时机一致 |
| `IResettable` 不感知池 | 对象声明内生状态清理能力，不持有池引用，在非池化上下文中行为不变；与池的耦合为零 |
| `RegisterPool<T>()` 封装 resolver | `IObjectResolver` 只在 Installer 的注册工厂闭包内出现；Presenter 构造函数只看到 `IObjectPool<T>`，不接触 DI 基础设施 |
| `FixedTotalSize` / `MaxInactiveSize` 命名分离 | 两种容量语义各有名称：前者限制总实例数（预算约束），后者限制闲置保留数（弹性场景）；Unity 的单一 `maxSize` 参数混合了两种含义 |
| `CollectionCheck` 与 DEBUG 绑定 | 开发期捕获双归还 bug，Release 构建零开销 |

| 代价 | 说明 |
|---|---|
| 没有 Dispose 自归还 | 消费方必须持有池引用并在正确时机显式调用 `Return()`；无法使用 `using` 块自动归还 |
| `IResettable` 无参数 | 需要外部上下文的重置（如归位到特定坐标）无法通过接口表达，必须通过 `onReturn` 委托补充，导致同一对象的重置逻辑分散在两处 |
| 不追踪 active 实例 | `Dispose()` 不能强制回收正在使用的实例；所有者必须在 Dispose 前完成归还，否则泄漏 |
| 无 `ExpandByDoubling` | 每帧数十次 Spawn 的极高频场景面临更多小 GC spike，无法通过翻倍策略批量前移分配开销 |
| `RegisterPool<T>()` 只封装标准路径 | 非标准 create（随机 prefab、后处理步骤）或自定义 destroy（Addressables）必须退出扩展方法手动注册，重新暴露 resolver 细节 |

---

## 已确认的设计意图

- **`IResettable.ResetState()` 在 `Return()` 而非 `Get()` 时调用**：是有意为之。原因：inactive 栈中的实例保持干净状态，`Get()` 直接使用无额外开销；若在 `Get()` 时调用，预热阶段首次 Get 的干净实例会被无意义地重置一次。"归还即清理"语义也与 Unity 的 `actionOnRelease` 一致，减少认知切换。

- **`SetActive` 不通过委托暴露给消费方**：是有意为之。原因：`ComponentPool<T>` 保证 `onReturn`/`ResetState()` 先于 `SetActive(false)` 执行，确保 `OnDisable` 触发时对象已完成状态重置。若消费方在 `onReturn` 委托中自行调用 `SetActive`，会与内置调用产生顺序竞争；封装此调用是防止误用的边界，不是功能限制。

- **`RegisterPool<T>()` 不暴露 `onDestroy` 参数**：是有意为之。原因：`Object.Destroy(v.gameObject)` 覆盖了所有非 Addressables 场景，将其暴露只会扩大扩展方法签名的认知负担。需要自定义 destroy 的场景（Addressables 等）已脱离"标准路径"定义，退出到手动注册是正确边界，与 `RegisterMVVM` 不覆盖非 Transient ViewModel 的取舍一致。

- **`FixedTotalSize` 设定时 `MaxInactiveSize` 被忽略**：是有意为之。原因：`FixedTotalSize` 的语义是"池永远保有全部预算实例，不销毁"，这与 `MaxInactiveSize` 的"超限时销毁"语义根本矛盾。允许两者并存会让行为取决于哪个先触发，产生难以预测的结果。互斥设计消除了歧义，语义清晰优先于灵活性。

- **`ObjectPool<T>.Dispose()` 不追踪 active 实例**：是有意为之。原因：追踪 active 实例需要 `HashSet<T>` 或引用计数，对每次 `Get()`/`Return()` 引入额外开销；活跃实例的生命周期是所有者的职责，框架代为管理会模糊这一边界，并无法给出有意义的错误信息（无法知道是谁持有了泄漏的实例）。

- **`CollectionCheck` 默认值与 `DEBUG` 符号绑定**：是有意为之。原因：CollectionCheck 的线性扫描在高频 Return 场景中开销不可忽视；其价值仅在开发期捕获编程错误（双归还），Release 构建中此类错误应在开发期已消除。与 Unity 自身 `collectionCheck = true` 默认值的立场一致，但将"是否开启"的决定交给编译期而非运行时配置。

- **`IResettable` 由池在 `Return()` 内部通过 `as` 转型自动检测，不要求显式注册**：是有意为之。原因：显式注册（在 `RegisterPool<T>()` 中声明"此类型实现了 IResettable"）会产生冗余——接口本身已经是声明。自动检测让接口成为唯一的契约点，消费方只需实现接口，无需在注册处重复表达。

---

## 已知与设计意图的偏差

- **`IResettable.ResetState()` 覆盖范围不完整**：设计意图是让对象完整表达内生状态的清理合约，但 `ResetState()` 无参数，当内生状态的合法初始值依赖外部数据（如出生坐标由 Spawner 决定）时，接口无法覆盖这部分重置逻辑，需退化为 `onReturn` 委托补充。结果是同一对象的重置职责在接口（处理固定初始值字段）和委托（处理依赖外部上下文的字段）之间分散，接口作为"完整清理合约"的表达力不足。
