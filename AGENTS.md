# Ruka Framework — AGENTS.md

## Local config

任务开始前**必须优先读取** `AGENTS.local.md`。
该文件中定义了所有形如`$SYMBOL`的符号的实际值。

---

Unity 自定义架构框架，面向中等复杂度商业游戏，提供 DI、逻辑时钟、类型安全 ID、资产管理、窗口系统等跨项目通用模块。

Unity 版本：`2022.3.20f1 LTS`

---

## Reference paths

以下路径已开启访问权限，按需读取：

- `$PATH_WORK_VAULT` — 设计文档、工作流、模块规范
- `$PATH_OLD_CONSUMER` — 旧消费方游戏项目（行为参考，只读）
- `$PATH_OLD_FRAMEWORK` — 旧框架实现（只读参考）

---

## Commands

- 当前无自动化测试
- 该项目无需构建脚本
- 不启用VContainer代码生成

---

## Non-obvious conventions

### API 查证

- 调用任何第三方库 API 前，优先使用 Context7 查证签名、命名空间、返回值，不凭记忆猜测
- 首次使用某个库的方法时，先 `context7_resolve-library-id` → `context7_query-docs`
- 禁止在未查证的情况下假设 API 存在

### 网络搜索

- 项目提供`mcp-searxng`，可使用`searxng_web_search`和`web_url_read`工具对外部信息进行搜索和验证

### 仓库结构

Ruka 是用于打包发布Unity Package的项目的仓库，不是常规 Unity 游戏项目。

仓库根目录核心结构：

- `Assets/Ruka/` — 框架源码根目录
  - `package.json` — 定义包信息和依赖
- `README.md`

禁止在 `Packages/` 目录下建立框架代码结构

### ASMDEFINE — 约定 ASMDEF 的命名和划分原则

- `Ruka.Utils` — 工具模块，包含各种通用工具类和扩展方法，不依赖其他模块
- `Ruka.Core` — 核心模块，包含基础和逻辑层设施
- `Ruka.UI` — UI 模块，包含表现层设施
- `Ruka.Editor` — 编辑器模块，包含编辑器扩展

### VContainer — 生命周期与 Scope

- **严禁单例模式**
- 优先采用DI框架和**纯C#实现**，**禁止直接使用 `MonoBehaviour` 进行逻辑编写**
- **严禁容器污染**：容器内注册的类型应当是消费方有理由直接注入的**业务服务**，而非内部管线数据；不被外部模块使用的数据应当通过 `.WithParameter()` 传递，不得成为容器的公开解析项
- `MonoBehaviour` 不能构造函数注入，统一使用 `[Inject]` 方法或字段注入
- `ScriptableObject` 不应当注入
- DI框架应当遵守`Core.DI`模块对VContainer的扩展实现，严格遵守`NestedLifetimeScope`,`IFeatureInstaller`,`FeatureGroupCollector`,`FeatureConfig`等协议进行注册和发现
- 严禁直接继承原生`LifetimeScope`; 除非注册项为局部专有，否则禁止创建`NestedLifetimeScope`子类,而应当遵守`InstallerGroup`分组协议
- 当注册类需要参与初始化(`IInitializable`,`IStartable` etc.)或Update/Tick生命周期时，必须通过`RegisterEntryPoint`API注册

### R3 — 订阅生命周期

- 所有 Subscribe 必须绑定生命周期，禁止裸 Subscribe
- Subscribe通用生命周期绑定
  - MonoBehaviour: `.AddTo(this.GetCancellationTokenOnDestroy())`
  - C#类：实现`IDisposable`接口，接入`CompositeDisposable`,并统一在 `Dispose` 中释放
- R3 <-> UnitTask（异步）互转标准：
  - `await observable.FirstAsync(cancellationToken);`
  - `Observable.FromAsync`
  - 禁止`.ToTask()`

### UniTask — 异步边界

- 统一使用 UniTask，禁止原生 `System.Threading.Task`（IL2CPP 不兼容）
- 禁止 `async void`；必须传入 CancellationToken 以确保可取消
- CancellationToken 来源：
  - MonoBehaviour: `this.GetCancellationTokenOnDestroy()`
  - C#类：自主创建 `CancellationTokenSource`，并在适当时机调用 `Cancel()` 以释放资源
- 禁止在非主线程访问 Unity API

### Symbol — ID协议

- 统一使用 `Symbol<T>` 和marker struct作为类型安全 ID，禁止使用裸字符串
- 提供 `Symbol<T>` 常量的静态类**必须**标注 `[SymbolProvider]`
- 每个 `[SerializeField]` 的 `Symbol<T>` 字段**必须**标注 `[SymbolSelector]`
- `SymbolSelector` 的 `AllowManualInput` 参数根据值集合是否封闭进行决策

### MessagePipe — 消息边界

- 使用`Singleton`消息模式，`Publisher`和`Subscriber`对应的消息包优先采用隐式注册
- 消息类型命名约定：`[Publisher模块名][事件描述]Msg`，如 `PlayerHealthChangedMsg`
- 使用 MessagePipe 的场景：跨模块，双方无显著依赖，需要解耦
- 使用 R3 Subject 的场景：模块内部，或模块间有显著依赖但不适合直接调用的情况（如频繁事件、状态流等）
- 两者不混用；如不确定，优先选择 R3 Subject

---

## Unity MCP

使用 `com.coplaydev.unity-mcp`，需要 Unity Editor 开启、HTTP 服务运行且 opencode 已连接。

### 可用工具

| 工具                              | 用途                                                                 |
| --------------------------------- | -------------------------------------------------------------------- |
| `manage_editor`                   | 查询编译状态（action: get_state），操作前必须确认 isCompiling: false |
| `refresh_unity`                   | 刷新 AssetDatabase                                                   |
| `read_console`                    | 读取 Console 日志                                                    |
| `create_script` / `delete_script` | 脚本文件的创建与删除                                                 |
| `script_apply_edits`              | 结构化 C# 方法/类编辑（insert/replace/delete）                       |
| `apply_text_edits`                | 精确文本编辑（带 hash 前置条件）                                     |
| `manage_asset`                    | 资产导入、更新、组织                                                 |
| `manage_components`               | 组件增删改查                                                         |
| `manage_scene`                    | 场景操作                                                             |
| `manage_packages`                 | 包管理                                                               |
| `execute_menu_item`               | 触发编辑器菜单项                                                     |
| `find_in_file`                    | 在项目文件内搜索内容                                                 |
| `run_tests`                       | 执行测试                                                             |
| `batch_execute`                   | 批量执行多个工具调用（最多 25 条/批），多步操作必须优先使用          |

---

## Boundaries

- 旧框架（`$PATH_OLD_FRAMEWORK`）只读，不修改任何文件
- 旧消费方项目（`$PATH_OLD_CONSUMER`）只读，不修改任何文件
- 不引入规范外的第三方库；需要新依赖时先确认再安装
- 禁止原生 `System.Threading.Task`
- 禁止裸 Subscribe（无生命周期绑定）
- **严禁 `FindObjectOfType`和`GetComponent`**
- 禁止Unity原生生命周期方法，采用DI框架的`Inject`和生命周期管理

### Unity 资产边界

- 严禁裸生成 `.meta` 文件，GUID 必须由 Unity Editor 自动生成
- 严禁裸生成 `.prefab` / `.unity` / `.asset` 文件
- 脚本（`.cs`）写入统一采用直接文件写入；仅当需要对较大已存在文件做局部方法级修改时，才使用 `script_apply_edits` 以避免重新生成整个文件
- 当前阶段性任务（e.g.一批脚本或文件写入）完成后必须调用 `refresh_unity`
- `.asmdef` 完成后必须调用 `refresh_unity` 并等待编译通过
- 需要资产操作但上方工具列表无法覆盖时，停止并告知用户手动完成

---

## Workflow

开始任何模块开发前：

- 读 `$PATH_WORK_VAULT`/workflow/agent_workflow.md`
- 如果对应模块有规范，读 `$PATH_WORK_VAULT`/specs/<模块名>.md`

实现时如需参考项目结构或旧实现：

- 在 `$PATH_OLD_FRAMEWORK` 中查找对应模块（只读）

如需理解现有用法：

- 在 `$PATH_OLD_CONSUMER` 中查找调用示例（只读）
