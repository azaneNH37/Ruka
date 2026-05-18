# Core.DI — 设计哲学

## 设计立场

### Zenject 的选择

Zenject 的核心理念是"DI 框架应当解决 Unity 开发中所有的集成问题"：MonoInstaller 直接继承 MonoBehaviour，固定的三层上下文层级（ProjectContext / SceneContext / GameObjectContext）与 Unity 场景管理一一对应，内置 Signals、MemoryPool、Factory、Validation。代价是重量级反射、IL2CPP 历史问题、以及高度陡峭的学习曲线。

### VContainer 的选择

VContainer 的核心理念是"DI 框架的职责只有一件事：构建对象图"。`Configure(IContainerBuilder)` 是纯 C# 方法，EntryPoint 通过 PlayerLoopSystem 而非 MonoBehaviour 挂载，三种 Lifetime 精确覆盖需求，可选 Source Generator 彻底消除反射。代价是"我应该在哪里注册 X"这个问题没有答案——框架不提供规约，只提供机制。

### Core.DI 的立场

Core.DI 接受 VContainer 的立场（薄、快、IL2CPP 安全），并在其上叠加一层**规约执行层**，专门解决"在多模块、跨项目场景下使用 VContainer 时反复出现的同类问题"：

- 注册散在 → 属性发现 + Collector 使注册自声明、可审计
- 环境 Config 变形 → `FeatureConfigOverride<T>` 提供类型安全的按 Scope 替换
- 跨场景 Scope 引用 → `ScopeRegistry` + `Symbol<ScopeIdentifier>` 提供声明式父解析
- IL2CPP stripping → `link.xml` 自动生成保护所有发现类型

Core.DI 明确**不**提供 Zenject 的 Signals、MemoryPool、Factory 等设施，这些职责委托给专职库（MessagePipe、R3）。

---

## 核心取舍

| 正面 | 说明 |
|---|---|
| 模块自声明 | 新功能只需在 Installer 上添加属性，不修改任何中央文件 |
| 类型安全的 Group 身份 | `InstallerGroupMarker` + `TypeFilter` 使 Group 选择器在 Inspector 和编译期都受约束 |
| Config 变形可组合 | 多个 `FeatureConfigOverride<T>` 按列表顺序叠加，各 Scope 独立，互不干扰 |
| IL2CPP 自动保护 | `link.xml` 由 Editor 自动生成，消除人工维护成本 |
| 发现链可审计 | `INSTALLER_MANIFEST.md` 提供跨 Group 的 Installer 索引，无需运行游戏即可检查 |

| 代价 | 说明 |
|---|---|
| 注册不可静态追踪 | 读 `GroupedLifetimeScope` 源码看不到实际注册内容；需查阅 manifest 或 Editor Foldout |
| 两阶段管线陈腐化风险 | Collector asset 内容由 domain reload 更新；若更新未完成，运行时持有旧类型列表 |
| Collector 手动挂载断层 | Editor 自动创建 Collector asset，但不自动挂载至 Scope；缺失步骤静默不报 |
| Installer 不可合成 | `Activator.CreateInstance` 限制 Installer 无法有构造参数，阻止 Installer 间依赖 |
| 规约学习成本前置 | 新接触者需理解属性→Collector→Scope 三段链路后才能有效参与 |

---

## 已确认的设计意图

- **`ScopeRegistry.Instance` 是静态的**：这是有意为之，原因是 Scope 需要在自身 `Awake` 中（容器构建之前）解析父 Scope，此时容器不存在，无法通过 DI 获取 Registry。静态 Bootstrap 基础设施与"业务服务禁止单例"约定不冲突。

- **`Activator.CreateInstance` 限制 Installer 无参**：有意限制。Installer 的职责是纯注册，不应携带运行时依赖。需要外部数据的注册应通过 `IFeatureConfig` + `FeatureConfigOverride<T>` 解决，而非让 Installer 自身成为有依赖的对象。

- **`ConfigOverrideBuildContext.Current` 是静态环境变量**：有意为之。它的生存期严格限定在 `GroupedLifetimeScope.Configure()` 的同步调用栈内（try/finally 保证清理），等价于局部变量的安全性。Unity 单线程模型下不存在并发风险。选择此方式是为了保持 `IFeatureInstaller.Install(IContainerBuilder)` 接口的清洁，不强迫消费方感知 override 机制。

- **`parentScopeId` 解析失败时静默回退**：有意为之。若目标 Scope 尚未注册（时序问题），回退到 VContainer 根而非抛出，是为了不在场景加载顺序变化时产生崩溃。`logParentResolution` 字段专门用于调试此类情况。

- **`ScopeRegistry` 碰撞时抛出**：有意为之。`scopeId` 的语义是"此 Scope 在全局中拥有唯一语义名称"，隐含唯一性断言。碰撞必然是设计错误或机制误用，应快速失败而非静默覆盖。

- **`FeatureInstallerAttribute` 使用 `Type` 而非 `string`**：有意为之。C# attribute 参数必须是编译期常量，`Symbol<T>` struct 不满足条件。使用 `typeof(XGroup)` 提供编译期语法检查（拼写错误即编译失败），并允许 IDE 重命名安全追踪。运行时的 `IsAssignableFrom` 校验提供语义保障。

---

## 已知与设计意图的偏差

- **Collector → Scope 挂载无提示**：应为：Editor 在 Collector 创建或更新后检测到该 Group 无 Scope 消费时发出警告。当前为：自动创建 Collector 但不检查是否有 Scope 引用它，缺失步骤无任何反馈。

- **link.xml 无构建前同步校验**：应为：构建前验证 `link.xml` 内容与当前 manifest 一致，不一致时阻断或警告。当前为：仅在 domain reload 时更新，若中间有未触发 reload 的脚本变更，IL2CPP 构建可能使用过期的 link.xml 静默裁剪类型。
