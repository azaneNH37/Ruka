# Module: Core.Pool

## 职责

为高频实例化场景提供对象复用基础设施：以 `IObjectPool<T>` 作为统一访问接口，通过预分配与 `Get`/`Return` 周期降低运行期反复创建与销毁带来的 GC 压力。

`ObjectPool<T>` 面向纯 C# 对象，采用委托描述创建、归还和销毁行为；`ComponentPool<T>` 面向 `MonoBehaviour`，统一封装 `SetActive` 激活约定；`PoolBuilderExtensions.RegisterPool<T>` 将 VContainer 的 `IObjectResolver` 隐藏在 Installer 注册内部，使消费方只依赖 `IObjectPool<T>`。

## 非职责

- 不提供对象的 `Dispose` 自归还约定（即 `foo.Dispose()` = `pool.Return(foo)` 不属于本模块）
- 不提供扩展倍增（Doubling）策略；池满时按单个实例创建
- 不追踪已取出实例的引用；归还时机与归还行为由所有者负责
- 不提供跨多个消费方共享对象的中心路由机制（无 PoolManager）
- 不池化 ViewModel 或纯数据对象的 DI Scope 生命周期；Transient/Scoped 仍由 VContainer 管理
- 不提供线程安全保证；按 Unity 主线程单线程模型使用

## 公开 API

```csharp
namespace Ruka.Core.Pool
{
    /// <summary>
    /// Marker interface for objects that can reset their own intrinsic state.
    /// </summary>
    public interface IResettable
    {
        /// <summary>Resets intrinsic state to a clean baseline.</summary>
        void ResetState();
    }

    /// <summary>
    /// Unified pool access contract for high-frequency acquire/return workflows.
    /// </summary>
    public interface IObjectPool<T> : IDisposable
    {
        /// <summary>Acquires an instance from the pool, creating one if the inactive stack is empty.</summary>
        T Get();

        /// <summary>Returns an instance to the pool.</summary>
        void Return(T instance);

        /// <summary>Number of instances currently acquired via Get and not yet returned.</summary>
        int CountActive { get; }

        /// <summary>Number of instances sitting in the inactive stack, available for Get.</summary>
        int CountInactive { get; }
    }

    /// <summary>Thrown when a fixed-size pool has no inactive instances remaining.</summary>
    public sealed class PoolCapacityExceededException : Exception
    {
        public PoolCapacityExceededException(string message);
    }

    /// <summary>Controls pool capacity and validation behavior.</summary>
    public struct PoolSettings
    {
        /// <summary>Instances pre-created at construction time.</summary>
        public int InitialSize;

        /// <summary>Maximum number of inactive instances retained in the stack.</summary>
        public int MaxInactiveSize;

        /// <summary>Hard cap on total instances ever created.</summary>
        public int? FixedTotalSize;

        /// <summary>Enables duplicate-return detection against the inactive stack.</summary>
        public bool CollectionCheck;

        /// <summary>Default settings. CollectionCheck is enabled in DEBUG builds.</summary>
        public static readonly PoolSettings Default;
    }

    /// <summary>
    /// Delegate-driven object pool for plain C# types.
    /// </summary>
    public class ObjectPool<T> : IObjectPool<T>
    {
        public ObjectPool(
            Func<T> create,
            Action<T> onReturn = null,
            Action<T> onDestroy = null,
            PoolSettings settings = default);

        public virtual T Get();
        public virtual void Return(T instance);
        public int CountActive { get; }
        public int CountInactive { get; }
        public virtual void Dispose();
    }

    /// <summary>
    /// ObjectPool variant for MonoBehaviour components.
    /// </summary>
    public class ComponentPool<T> : ObjectPool<T> where T : MonoBehaviour
    {
        public ComponentPool(
            Func<T> create,
            Action<T> onReturn = null,
            Action<T> onDestroy = null,
            PoolSettings settings = default);

        public override T Get();
    }

    /// <summary>
    /// IContainerBuilder extensions for standard component pool registration.
    /// </summary>
    public static class PoolBuilderExtensions
    {
        /// <summary>
        /// Registers ComponentPool{T} as a Singleton IObjectPool{T}.
        /// </summary>
        public static void RegisterPool<T>(
            this IContainerBuilder builder,
            T prefab,
            Transform poolRoot,
            PoolSettings settings = default,
            Action<T> onReturn = null)
            where T : MonoBehaviour;
    }
}
```

## 最小使用示例

```csharp
// 1. 组件自身只声明可清理的内生状态，不持有池引用
public sealed class BulletView : MonoBehaviour, IResettable
{
    [SerializeField] private Rigidbody _rb;

    void IResettable.ResetState()
    {
        _rb.linearVelocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
    }
}

// 2. Installer 内注册标准组件池
public sealed class BulletInstaller : IFeatureInstaller
{
    [SerializeField] private BulletView _prefab;
    [SerializeField] private Transform _poolRoot;

    public void Install(IContainerBuilder builder)
    {
        builder.RegisterPool(
            _prefab,
            _poolRoot,
            new PoolSettings { InitialSize = 20, MaxInactiveSize = 50 });
    }
}

// 3. 消费方注入接口，不接触 IObjectResolver
public sealed class BulletSpawner
{
    private readonly IObjectPool<BulletView> _pool;

    public BulletSpawner(IObjectPool<BulletView> pool)
    {
        _pool = pool;
    }

    public BulletView Spawn(Vector3 position)
    {
        var bullet = _pool.Get();
        bullet.transform.position = position;
        return bullet;
    }

    public void Despawn(BulletView bullet)
    {
        _pool.Return(bullet);
    }
}

// 4. 非标准创建或销毁逻辑时退出 RegisterPool，手动注册
builder.Register<IObjectPool<EnemyView>>(
    resolver => new ComponentPool<EnemyView>(
        create: () => resolver.Instantiate(_prefabs[Random.Range(0, _prefabs.Length)], _poolRoot),
        onReturn: view => view.transform.position = Vector3.zero,
        settings: new PoolSettings { InitialSize = 10 }),
    Lifetime.Singleton);
```

## 关键设计约束

- **`IResettable` 不感知池**：对象只表达“我可以清理自己的内生状态”，不持有池引用，也不通过 `Dispose` 自归还。池在 `Return()` 内部通过 `is IResettable` 自动检测并调用。
- **归还顺序固定**：`ObjectPool<T>.Return()` 的顺序为 `IResettable.ResetState()` → `onReturn` → 入栈或销毁。`ComponentPool<T>` 在此之后执行 `SetActive(false)`。
- **组件获取后激活**：`ComponentPool<T>.Get()` 从池中取出实例后执行 `SetActive(true)`；预热和新创建出来的实例在进入 inactive 栈前会先 `SetActive(false)`。
- **`FixedTotalSize` 优先于 `MaxInactiveSize`**：设置 `FixedTotalSize` 时，总实例数被严格限制；所有实例均 active 后再次 `Get()` 抛出 `PoolCapacityExceededException`。未设置 `FixedTotalSize` 时，`MaxInactiveSize` 控制归还时保留或销毁。
- **`CollectionCheck` 仅做重复归还检测**：开启时，`Return()` 会线性扫描 inactive 栈并在重复归还时抛出 `InvalidOperationException`。默认值在 `DEBUG` 构建中开启，在非 `DEBUG` 构建中关闭。
- **`RegisterPool<T>()` 只覆盖标准路径**：它固定使用 `resolver.Instantiate(prefab, poolRoot)` 创建实例，默认销毁行为由 `ComponentPool<T>` 使用 `Object.Destroy(instance.gameObject)`。随机 prefab、Addressables 自定义释放等非标准行为应手动注册。
- **`Dispose()` 不处理 active 实例**：池只销毁 inactive 栈内实例；已 `Get()` 未 `Return()` 的实例由所有者负责归还或销毁。

## 依赖

- `VContainer` — `IContainerBuilder`、`IObjectResolver`、`Lifetime`
- `VContainer.Unity` — `Instantiate(prefab, parent)` 扩展方法
- `UnityEngine` — `MonoBehaviour`、`Transform`、`Object.Destroy`
