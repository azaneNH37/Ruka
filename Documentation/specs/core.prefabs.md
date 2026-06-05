# Module: Core.Prefabs

## 职责

通过 `IPrefabFactory` 提供上下文感知的预制件实例化管线：将资产加载（YooAsset `IAssetScope`）、DI 注入（VContainer `LifetimeScope` / `IObjectResolver`）、transform 层级挂载、激活时机和实例生命周期作为独立可组合的轴暴露给调用方，使 Visual Parent 与 DI Parent 可以独立指定。管线内部对实例 GO 树执行 scope-boundary-aware 深度遍历，保证每个组件被其所属容器注入，scope 边界作为注入权威边界，调用方无需感知 prefab 内部的 scope 拓扑。

## 非职责

- 不管理对象池化——高频复用场景使用 `Core.Pool`，本模块每次调用创建新实例
- 不提供同步实例化——资产加载始终异步，返回 `UniTask`
- 不执行 DDOL（`DontDestroyOnLoad`）——框架不鼓励 DDOL 对象，需要时由调用方自行处理
- 不提供销毁动画编排——上层模块在 `Dispose` 前自行处理过渡动画
- 不追踪已发出的 `PrefabInstanceHandle` 集合——释放责任在所有者（handle 持有方或 Scope CT）

## 公开 API

```csharp
namespace Ruka.Core.Prefabs
{
    /// <summary>
    /// Context-aware prefab instantiation pipeline. Not a replacement for
    /// <c>Object.Instantiate</c> — use this when the instance needs DI injection,
    /// scoped asset tracking, or a visual parent that differs from the DI parent.
    /// </summary>
    public interface IPrefabFactory
    {
        /// <summary>Loads and instantiates a prefab, returning an ownership handle.</summary>
        /// <param name="key">YooAsset address of the prefab.</param>
        /// <param name="configure">Optional callback to set visual parent, DI parent, installers, position, etc.</param>
        /// <param name="ct">
        /// Cancels the async load when pending; after instantiation, binds instance lifetime
        /// so the GO is destroyed when the token fires.
        /// Defaults to the owning scope's destroyCancellationToken, not CancellationToken.None.
        /// </param>
        UniTask<PrefabInstanceHandle> InstantiateAsync(
            Symbol<AssetRef> key,
            Action<PrefabOptions> configure = null,
            CancellationToken ct = default);

        /// <summary>Typed overload that also extracts the root component.</summary>
        /// <remarks>Throws InvalidOperationException if the root GO lacks component T.</remarks>
        UniTask<PrefabInstanceHandle<T>> InstantiateAsync<T>(
            Symbol<AssetRef> key,
            Action<PrefabOptions> configure = null,
            CancellationToken ct = default)
            where T : Component;
    }

    /// <summary>
    /// Fluent builder for prefab instantiation options. Passed via Action callback;
    /// do not cache or reuse instances across calls.
    /// </summary>
    public class PrefabOptions
    {
        /// <summary>Sets the transform parent (visual hierarchy only, does not affect DI parent).</summary>
        PrefabOptions Under(Transform parent);

        /// <summary>Overrides the DI parent scope. Defaults to the scope that owns the IPrefabFactory.</summary>
        PrefabOptions WithDiParent(LifetimeScope scope);

        /// <summary>Enqueues extra registrations into the prefab's LifetimeScope during activation.</summary>
        /// <remarks>Only effective for scope prefabs. Ignored for plain prefabs.</remarks>
        PrefabOptions WithInstallation(Action<IContainerBuilder> installation);

        /// <summary>Same as above, accepting a pre-built IInstaller.</summary>
        PrefabOptions WithInstallation(IInstaller installer);

        /// <summary>Prevents the factory from activating the GO.</summary>
        /// <remarks>
        /// When combined with WithInstallation on a scope prefab, the caller must
        /// call LifetimeScope.Enqueue before manually activating.
        /// </remarks>
        PrefabOptions ManualActivation();

        /// <summary>Preserves world position when parenting via Under.</summary>
        PrefabOptions WorldPositionStays();

        /// <summary>Sets world position for the instance.</summary>
        PrefabOptions At(Vector3 position);

        /// <summary>Sets world position and rotation for the instance.</summary>
        PrefabOptions At(Vector3 position, Quaternion rotation);
    }

    /// <summary>
    /// Owns a prefab instance and its asset reference. Disposing destroys the GO
    /// and releases the loaded asset. Idempotent.
    /// </summary>
    public class PrefabInstanceHandle : IDisposable
    {
        /// <summary>The instantiated root GameObject.</summary>
        GameObject GameObject { get; }

        /// <summary>The child LifetimeScope on the instance root, or null for plain prefabs.</summary>
        LifetimeScope Scope { get; }

        void Dispose();
    }

    /// <summary>Typed variant that also exposes the root component.</summary>
    public class PrefabInstanceHandle<T> : PrefabInstanceHandle where T : Component
    {
        /// <summary>The component on the instance root, guaranteed non-null at construction.</summary>
        T Component { get; }
    }
}
```

## 最小使用示例

```csharp
// ─── 基础用法：加载 + 注入 + 自动释放 ─────────────────────────────
public class UnitSpawner : IDisposable
{
    private readonly IPrefabFactory _prefabs;

    public UnitSpawner(IPrefabFactory prefabs)
    {
        _prefabs = prefabs;
        // IPrefabFactory 是 Scoped，从哪个 Scope 注入就以该 Scope 为默认 DI Parent。
        // Scope 销毁时，factory 的 CT 触发，所有未手动 Dispose 的 handle 自动回收。
    }

    public async UniTask<PrefabInstanceHandle> SpawnAsync(Symbol<AssetRef> unitKey, Vector3 pos, CancellationToken ct)
    {
        // 最简调用：无 configure 回调 → 无 visual parent、DI parent = 当前 scope
        return await _prefabs.InstantiateAsync(unitKey, o => o.At(pos), ct);
    }

    public void Dispose() { }
}

// ─── Visual Parent ≠ DI Parent：窗口挂在 DDOL Canvas，注入来自 Session ──
public class WindowOpener
{
    private readonly IPrefabFactory _prefabs;
    private readonly LifetimeScope _sessionScope;

    public WindowOpener(IPrefabFactory prefabs, LifetimeScope sessionScope)
    {
        _prefabs = prefabs;
        _sessionScope = sessionScope;
    }

    public async UniTask<PrefabInstanceHandle<WindowBase>> OpenAsync(
        Symbol<AssetRef> prefabKey, Transform canvasRoot, CancellationToken ct)
    {
        return await _prefabs.InstantiateAsync<WindowBase>(
            prefabKey,
            o => o.Under(canvasRoot)
                  .WithDiParent(_sessionScope)
                  .WithInstallation(b => b.RegisterInstance(new SomePayload())),
            ct);
    }
}

// ─── 注册：无需额外操作，PrefabInstaller 通过 [FeatureInstaller] 自动生效 ──
// PrefabInstaller 在 ProjectGroup (order: 25) 注册 PrefabFactory 为 Lifetime.Scoped。
// 每个 LifetimeScope 注入的 IPrefabFactory 实例自动以该 Scope 为默认 DI Parent。
```

## 关键设计约束

- **`Lifetime.Scoped` 即默认 DI Parent**：`PrefabFactory` 注册为 `Scoped`，从哪个 Scope 注入就以该 Scope 的 `LifetimeScope` 为默认 DI Parent 和默认 CT 来源。不需要每次调用都传 `WithDiParent`。
- **CT 跟随 DI Parent**：`ct` 参数在异步加载阶段用作操作取消；实例化完成后，同一 token 被绑定为实例生命周期——token 触发时 handle 自动 Dispose。传入 `default` 时回退到 **DI parent**（`WithDiParent` 覆盖后的 scope，或 factory 所属 scope）的 `destroyCancellationToken`，而非 `CancellationToken.None`。这保证 `WithDiParent(X)` 时实例生命周期自动对齐到 X，避免 DI parent 与 CT 来源不一致导致实例早亡。
- **Scope-boundary-aware 注入**：管线对实例 GO 树执行深度优先遍历。无 `LifetimeScope` 的 GO 由调用方容器逐组件注入；遇到 `LifetimeScope` 时设定其 DI parent 并跳过该子树（由 scope 自行管辖）。根节点有 scope 只是遍历首步即命中 scope 规则的特例，不再是独立路径。调用方无需感知 prefab 内部的 scope 拓扑。
- **`ManualActivation` + `WithInstallation` 的陷阱**：`LifetimeScope.Enqueue` 使用全局静态栈，在 `Awake`（即 `SetActive(true)`）时消费。如果 `ManualActivation` 为 true，factory 不会激活 GO，也不会 Enqueue installer。调用方必须在手动激活前自行 `LifetimeScope.Enqueue`。
- **每实例独立 `IAssetScope`**：每次 `InstantiateAsync` 创建一个子 `IAssetScope` 来追踪加载的 prefab 资产。`handle.Dispose()` 释放该子 scope，归还资产引用。父 `IAssetScope` 不受影响。
- **失败自动回滚**：管线中任何步骤抛异常（加载失败、CT 取消、组件缺失），已创建的 GO 会被 `Object.Destroy`，子 `IAssetScope` 会被 Dispose，异常重新抛出。
- **`PrefabReleaseHook` 安全网**：每个实例根节点附加一个内部 `MonoBehaviour`，在 `OnDestroy` 时调用 `handle.Dispose()`。当外部代码直接 `Destroy(go)` 时，资产引用仍能正确释放。与 CT 回调和手动 Dispose 三条路径均幂等收敛。

## 依赖

- `Ruka.Core.Resources` — `IAssetScope`、`Symbol<AssetRef>`：资产加载和引用追踪
- `Ruka.Core.DI` — `IFeatureInstaller`、`ProjectGroup`：自动注册基础设施
- `Ruka.Core.Symbols` — `Symbol<T>`：类型安全标识符
- `VContainer` — `IObjectResolver`、`IContainerBuilder`、`Lifetime`：DI 容器
- `VContainer.Unity` — `LifetimeScope`、`IInstaller`：scope 生命周期和 installer 接口
- `UniTask` — `UniTask<T>`、`CancellationToken`：异步管线
- `UnityEngine` — `GameObject`、`Object.Instantiate`、`Transform`、`Component`
