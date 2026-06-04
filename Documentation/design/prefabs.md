# Core.Prefabs — 设计哲学

## 设计立场

### VContainer `CreateChildFromPrefab` 的选择

VContainer 提供 `LifetimeScope.CreateChildFromPrefab(prefab)` 作为标准的 scope prefab 实例化方式。它在内部调用 `Object.Instantiate(prefab, this.transform, false)`，将实例的 transform 父级硬绑定到 DI parent scope 的 transform。这在大多数场景下是合理的——scope prefab 挂在创建它的 scope 下，层级清晰。

### Core.Prefabs 的立场

**明确背离 `CreateChildFromPrefab` 的 "visual parent = DI parent" 假设。**

Unity 的 transform 层级和 VContainer 的 scope 层级是两个独立的关注点。当两者恰好对齐时（如场景内的子系统），`CreateChildFromPrefab` 工作良好。但在以下真实场景中两者分离：

- 窗口 prefab 挂在 DDOL Canvas 下显示（visual parent），但属于 Session Scope 接受注入（DI parent）
- 跨场景的 HUD 元素挂在持久 Canvas 下，但属于当前 Phase 的 Scene Scope
- 动态 spawn 的 gameplay 对象需要特定世界坐标，但需要从当前战斗 Scope 注入

Core.Prefabs 通过复制 `CreateChildFromPrefab` 的核心逻辑（设置 `parentReference.Object` + `LifetimeScope.Enqueue` + `SetActive(true)` 触发 Build）并替换实例化调用，使 visual parent 和 DI parent 成为独立参数。

## 核心取舍

| 特性 | 说明 |
|---|---|
| Visual Parent / DI Parent 完全独立 | 调用方可以将实例挂在任意 transform 下，同时从任意 scope 注入 |
| 统一管线 | scope prefab 和 plain prefab 使用同一 API，内部自动分流 |
| 每实例资产追踪 | 子 `IAssetScope` 精确对应单个实例的资产生命周期 |
| CT 跟随 DI Parent | 默认 CT 取自 DI parent（覆盖后的或 factory scope），一个 token 同时控制异步取消和实例生命周期 |
| `Action<PrefabOptions>` 回调 | 声明式配置，默认值覆盖最常见场景，调用方只指定需要偏离的轴 |

| 代价 | 说明 |
|---|---|
| 复制 VContainer 内部逻辑 | `parentReference.Object` 和 `LifetimeScope.Enqueue` 虽然是 public API，但属于 VContainer 的低层机制，未来版本变更时需要同步更新 |
| `ManualActivation` + `WithInstallation` 的语义裂缝 | `LifetimeScope.Enqueue` 使用全局静态栈，factory 无法在不立即激活的情况下持久化 installer；manual activation 场景下责任转移给调用方 |
| 无同步路径 | 即使 prefab 已缓存，调用方仍需 await；对帧内批量 spawn 场景不如直接 `Object.Instantiate` + 手动注入高效 |
| 单 CT 无法区分"取消操作"与"杀死实例" | 如果调用方需要取消加载但保留已创建实例，当前 API 无法表达 |
| CT 隐式切换来源 | `WithDiParent(X)` 会将默认 CT 从 factory scope 切换到 X；调用方传显式 CT 时不受影响，但未传 CT 时生命周期可能不符合直觉（跟了 DI parent 而非 factory scope）|

## 已确认的设计意图

- **`ct = default` 回退到 DI parent CT 而非 `CancellationToken.None`** 是有意为之，原因：如果回退到 None，忘记传 CT 的调用方会创建出永生实例，scope 销毁后资产泄漏。回退到 DI parent CT 使遗忘传参的代价为"实例随所有者 scope 销毁"而非"资产泄漏"。当 `WithDiParent(X)` 被设置时，CT 跟随 X 而非 factory scope，保证 DI 归属和生命周期一致。
- **prefab 在实例化前被临时 `SetActive(false)` 然后在 finally 中恢复** 是有意为之，原因：防止 `Object.Instantiate` 时触发 `Awake`，确保 `parentReference` 和 `Enqueue` 在 `Awake` 之前完成设置。
- **不提供 `DestroyAsync` 方法** 是有意为之，原因：销毁前的过渡动画属于上层模块的关注点（窗口系统、gameplay 模块），factory 只负责创建和释放。上层在动画完成后调用 `handle.Dispose()` 即可。
- **`PrefabReleaseHook` 使用 `OnDestroy` 而非 `IDisposable`** 是有意为之，原因：hook 必须拦截外部代码对 GO 的 `Object.Destroy` 调用，这只能通过 `MonoBehaviour.OnDestroy` 实现。

## 已知与设计意图的偏差

- **`ManualActivation` + scope prefab + `WithInstallation`**：应为 factory 在延迟激活时仍能保证 installer 被正确注入，当前为调用方必须自行 `Enqueue`。根本原因是 `LifetimeScope.Enqueue` 的全局静态栈语义不支持跨帧持久化。
