# Module: Core.DI

## 职责

通过属性扫描（`[FeatureInstaller(typeof(XGroup))]`）在编译期发现 `IFeatureInstaller` 实现，经由 ScriptableObject Collector 将发现结果桥接至运行时，在 `GroupedLifetimeScope` 构建时按组批量执行注册；同时提供 Symbol 命名 Scope 解析、FeatureConfig 变形注入和 IL2CPP stripping 保护，使 VContainer 的注册管理在多模块跨项目场景下保持可发现、可审计、规约一致。

## 非职责

- 不提供消息总线或事件系统（→ MessagePipe）
- 不提供响应式流（→ R3）
- 不提供运行时工厂、对象池、内存管理
- 不替代 VContainer 的 `IContainerBuilder` API；所有注册最终通过 VContainer 完成
- 不管理 Scope 的生命周期结束（→ VContainer `LifetimeScope.Dispose`）

## 公开 API

```csharp
namespace Ruka.Core.DI
{
    // ─── Group Markers ────────────────────────────────────────────────────────

    /// <summary>Base class for installer group markers. Inherit to declare a custom group.</summary>
    public abstract class InstallerGroupMarker { }

    /// <summary>Built-in installer group for project-lifetime registrations.</summary>
    public sealed class ProjectGroup : InstallerGroupMarker { }

    /// <summary>Built-in installer group for scene-lifetime registrations.</summary>
    public sealed class SceneGroup : InstallerGroupMarker { }

    /// <summary>Built-in installer group for session-lifetime registrations.</summary>
    public sealed class SessionGroup : InstallerGroupMarker { }

    // ─── Installer Protocol ───────────────────────────────────────────────────

    /// <summary>
    /// Ruka's primary registration contract for modular feature installation.
    /// Not a replacement for VContainer's IInstaller — IFeatureInstaller is the persistent,
    /// group-based main path; VContainer's IInstaller is for transient cross-scene enqueue.
    /// </summary>
    public interface IFeatureInstaller
    {
        /// <summary>Register services into the container builder. Must not access runtime state.</summary>
        void Install(IContainerBuilder builder);
    }

    /// <summary>Marks an IFeatureInstaller class for automatic discovery by group.</summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class FeatureInstallerAttribute : Attribute
    {
        public FeatureInstallerAttribute(Type group, int order = 0);

        /// <summary>The group marker type this installer belongs to.</summary>
        public Type Group { get; }

        /// <summary>Execution order within the group. Keep at 0 unless resolving a last-wins conflict.</summary>
        public int Order { get; }
    }

    // ─── Config Override Protocol ─────────────────────────────────────────────

    /// <summary>Marker interface for plain-data config structs registered via RegisterConfig.</summary>
    public interface IFeatureConfig { }

    /// <summary>
    /// ScriptableObject that substitutes or patches a single T config during scope build.
    /// Not a service — attach to GroupedLifetimeScope.configOverrides to activate.
    /// </summary>
    public abstract class FeatureConfigOverride<T> : ScriptableObject where T : IFeatureConfig
    {
        /// <summary>Produces the final config value from baseline. Do not read runtime state here.</summary>
        public abstract T Apply(T baseline);
    }

    // ─── Container Builder Extensions ─────────────────────────────────────────

    public static class ContainerBuilderExtensions
    {
        /// <summary>
        /// Registers baseline as a scoped T instance, applying any matching FeatureConfigOverride
        /// from the active scope before registration. Call inside IFeatureInstaller.Install only.
        /// </summary>
        public static void RegisterConfig<T>(this IContainerBuilder builder, T baseline)
            where T : IFeatureConfig;
    }

    // ─── Scope Assets ─────────────────────────────────────────────────────────

    /// <summary>
    /// ScriptableObject that maps an installer group to its discovered IFeatureInstaller types.
    /// Populated automatically by FeatureInstallerManifestProcessor on each domain reload.
    /// </summary>
    public sealed class FeatureGroupCollector : ScriptableObject
    {
        /// <summary>The installer group this collector serves.</summary>
        public Type TargetGroup { get; }

        /// <summary>Installer types discovered for this group. Do not modify directly.</summary>
        public IReadOnlyList<SerializableType> QualifiedTypes { get; }
    }

    // ─── Scope MonoBehaviours ─────────────────────────────────────────────────

    /// <summary>
    /// A LifetimeScope with Symbol-based scope naming, flexible parent resolution,
    /// and automatic SelfInjectMarker scanning.
    /// </summary>
    public class NestedLifetimeScope : LifetimeScope
    {
        // Inspector fields (configure in Prefab/Scene):
        // Symbol<ScopeIdentifier> scopeId         — unique name; leave empty to skip registration
        // bool autoParent = true                  — walk transform hierarchy for parent
        // LifetimeScope parentScope               — explicit parent; used when autoParent is false
        // Symbol<ScopeIdentifier> parentScopeId   — Symbol lookup fallback; used when autoParent is false and parentScope is null
        // bool logParentResolution                — debug log on Awake
    }

    /// <summary>
    /// A NestedLifetimeScope that installs services from FeatureGroupCollector assets,
    /// with optional FeatureConfigOverride substitution.
    /// </summary>
    public class GroupedLifetimeScope : NestedLifetimeScope
    {
        // Inspector fields:
        // List<FeatureGroupCollector> collectors   — collectors whose installers will run
        // List<ScriptableObject> configOverrides   — FeatureConfigOverride<T> assets to apply
    }

    /// <summary>
    /// Marks a GameObject for automatic injection by the nearest enclosing NestedLifetimeScope.
    /// </summary>
    public sealed class SelfInjectMarker : MonoBehaviour { }

    // ─── Scope Registry ───────────────────────────────────────────────────────

    /// <summary>Phantom type for Symbol-based scope name identifiers.</summary>
    public struct ScopeIdentifier { }

    /// <summary>Framework-defined named scope identifiers.</summary>
    public static class ScopeIdentifiers
    {
        public static readonly Symbol<ScopeIdentifier> Session;
    }
}
```

## 最小使用示例

```csharp
// 1. Define a custom group (or use a built-in: ProjectGroup, SceneGroup, SessionGroup)
public sealed class GameplayGroup : InstallerGroupMarker { }

// 2. Define a config struct
public struct CombatConfig : IFeatureConfig
{
    public float BaseDamage;
    public int MaxTargets;
}

// 3. Write an installer — self-contained, no central file to touch
[FeatureInstaller(typeof(GameplayGroup))]
public sealed class CombatInstaller : IFeatureInstaller
{
    public void Install(IContainerBuilder builder)
    {
        // Config is registered with baseline; any attached FeatureConfigOverride<CombatConfig>
        // on the scope will substitute it transparently before RegisterInstance is called.
        builder.RegisterConfig(new CombatConfig { BaseDamage = 10f, MaxTargets = 3 });

        builder.Register<CombatService>(Lifetime.Scoped);
        builder.RegisterEntryPoint<CombatController>();
    }
}

// 4. Create a FeatureGroupCollector SO via menu: Ruka/DI/Feature Group Collector
//    Set its Target Group to GameplayGroup in the Inspector.
//    (FeatureInstallerManifestProcessor auto-creates and populates this on domain reload.)

// 5. Attach a GroupedLifetimeScope to a scene GameObject.
//    Drag the GameplayGroup Collector into its Collectors list.
//    At runtime, CombatInstaller.Install runs automatically when the scope builds.

// 6. Override config per-scope: create a ScriptableObject inheriting FeatureConfigOverride<CombatConfig>
public sealed class HardModeCombatOverride : FeatureConfigOverride<CombatConfig>
{
    public override CombatConfig Apply(CombatConfig baseline)
        => baseline with { BaseDamage = baseline.BaseDamage * 2f };
}
//    Attach the HardModeCombatOverride asset to the scope's Config Overrides list.
//    The scope resolves CombatConfig with BaseDamage = 20f; other scopes are unaffected.

// 7. Named scope parent resolution (cross-scene additive loading)
//    On the session scope Prefab: set Scope Id = ScopeIdentifiers.Session
//    On a scene scope Prefab: set autoParent = false, Parent Scope Id = ScopeIdentifiers.Session
//    The scene scope finds its parent by Symbol lookup at Awake, regardless of load order.

// 8. MonoBehaviour injection without Inspector wiring
//    Add SelfInjectMarker component to the GameObject; the enclosing NestedLifetimeScope
//    discovers it on Awake and adds it to autoInjectGameObjects automatically.
```

## 关键设计约束

- **Installer 必须无参构造**：`IFeatureInstaller` 实现通过 `Activator.CreateInstance` 实例化，因此不能有构造函数参数。这是有意限制——需要外部数据的注册逻辑应通过 `IFeatureConfig` + `FeatureConfigOverride<T>` 传入，而非在 installer 自身中引入依赖。

- **Scope ID 全局唯一**：任何时刻带有相同 `scopeId` 的两个 `NestedLifetimeScope` 同时存在，第二个的 `Awake` 会抛出 `InvalidOperationException`。动态大量创建的子 Scope（如 Enemy、Room）不应设置 `scopeId`，应使用 `autoParent` 或直接 `parentScope` 引用。

- **`parentScopeId` 依赖注册时序**：通过 `parentScopeId` 查找父 Scope 时，目标 Scope 必须已在 `ScopeRegistry` 中注册。若父 Scope 的 `Awake` 在子 Scope 之后执行，解析将失败并回退到 VContainer 根。`autoParent`（transform 层级）不存在此时序问题。

- **Collector 必须手动挂载至 Scope**：`FeatureInstallerManifestProcessor` 自动创建 Collector asset 并填充类型列表，但不会自动将 Collector 挂载到任何 `GroupedLifetimeScope`。这一人工步骤缺失时，installer 存在于 manifest 但永远不会运行。

- **`RegisterConfig<T>` 只应在 Install 内调用**：该方法读取 `ConfigOverrideBuildContext.Current`，此上下文只在 `GroupedLifetimeScope.Configure()` 调用栈内有效。在 scope 构建期外调用时，override 不会应用，但注册本身不会出错。

- **Installer 类型受 link.xml 保护**：Editor 在每次 domain reload 后自动更新 `Assets/Ruka.Generated/link.xml`，防止 IL2CPP 裁剪通过属性发现的 installer 类型。若 link.xml 未能正确生成，IL2CPP 构建会静默移除这些类型。

## 依赖

- `VContainer` — `LifetimeScope`、`IContainerBuilder`、`Lifetime`、EntryPoint 机制
- `Ruka.Core.Symbols` — `Symbol<T>`、`SymbolProvider`、`SymbolSelector`（Scope 命名）
- `Ruka.Utils.Core` — `SerializableType`、`TypeFilterAttribute`（Collector 字段序列化）
