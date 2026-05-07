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

### 仓库结构

Ruka 是独立的 Unity Package 仓库，不是某个 Unity 项目的子目录。

仓库根目录核心结构：

- `Assets/Ruka/` — 框架源码根目录
  - `package.json` — 定义包信息和依赖
- `README.md`

禁止在 `Packages/` 目录下建立框架代码结构——
Ruka 作为 package 被消费方项目引用，
其源码仓库本身不包含 Unity 项目的 `Assets/` 或 `Packages/` 目录。

### ASMDEFINE — 约定 ASMDEF 的命名和划分原则

- `Ruka.Utils` — 工具模块，包含各种通用工具类和扩展方法，不依赖其他模块
- `Ruka.Core` — 核心模块，包含基础和逻辑层设施
- `Ruka.UI` — UI 模块，包含表现层设施
- `Ruka.Editor` — 编辑器模块，包含编辑器扩展

### VContainer — 生命周期与 Scope

- 严禁单例模式
- 优先采用DI框架和纯C#实现，禁止直接使用 `MonoBehaviour` 进行逻辑编写
- `MonoBehaviour` 不能构造函数注入，统一使用 `[Inject]` 方法或字段注入
- `ScriptableObject` 不应当注入
- DI框架应当遵守`Core.DI`模块对VContainer的扩展实现，严格遵守`NestedLifetimeScope`,`IFeatureInstaller`,`FeatureGroupCollector`,`FeatureConfigBase`等协议进行注册和发现
- 严禁直接继承原生`LifetimeScope`; 除非注册项为局部专有，否则禁止创建`NestedLifetimeScope`子类,而应当遵守`InstallerGroup`分组协议

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

### MessagePipe — 消息边界

- 使用`Singleton`消息模式，`Publisher`和`Subscriber`对应的消息包优先采用隐式注册
- 消息类型命名约定：`[Publisher模块名][事件描述]Msg`，如 `PlayerHealthChangedMsg`
- 使用 MessagePipe 的场景：跨模块，双方无显著依赖，需要解耦
- 使用 R3 Subject 的场景：模块内部，或模块间有显著依赖但不适合直接调用的情况（如频繁事件、状态流等）
- 两者不混用；如不确定，优先选择 R3 Subject

---

## Unity MCP

使用 `com.ivanmurzak.unity.mcp`，**调用任何工具前确认 Unity Editor 已开启且插件已连接**。

### 前提检查

- 操作前调用 `editor-application-get-state` 确认编译已完成（`isCompiling: false`）
- 编译中时等待完成再继续，不要并发操作

### 已知 schema 兼容性问题（ReflectorNet PR #77 合并前）

以下工具因 .NET 泛型/数组类型 schema 序列化问题暂不可用：

- `assets-find` — 替代：`code-run` 调用 `AssetDatabase.FindAssets()`
- `assets-modify` — 替代：`code-run` 直接加载资产并修改字段
- `reflection-method-call` — 替代：`code-run` 直接调用目标方法
- 其他参数含集合或泛型类型的工具 — 统一替代：`code-run`

**遇到 Invalid schema 错误时的处理流程：**

1. 停止调用出错工具
2. 改用 `code-run` 编写等效 C# 代码直接执行
3. 不要尝试绕过错误重试同一工具

PR #77 合并并发布后，删除此节，恢复对应工具的正常使用。

### code-run 使用规范

- 代码必须定义一个包含静态方法的类
- Unity API 调用必须在主线程执行：
  使用 `MainThread.Instance.Run(() => { ... })`
- 编译前通过 `editor-application-get-state`
  确认 `isCompiling: false`
- 执行后检查返回值前缀：
  - `[Success]` — 正常
  - `[Error]` — 编译或运行时错误，根据信息修正后重试

### 必须通过 MCP 完成的操作

以下操作严禁裸文件生成，必须走 MCP 工具：

**资产**

- 创建资产 → `assets-create`
- 创建文件夹 → `assets-create-folder`
- 删除资产 → `assets-delete`
- 移动/重命名 → `assets-move`
- 复制资产 → `assets-duplicate`
- 读取资产数据 → `assets-get-data`
- 修改资产字段 → `assets-modify`
- 刷新 AssetDatabase → `assets-refresh`
- 创建 Material → `assets-material-create`

**Prefab**

- 从场景对象创建 → `prefab-create`
- 在场景中生成 → `prefab-spawn`
- 进入/退出/保存 Prefab Mode → `prefab-open` / `prefab-exit` / `prefab-save`

**场景**

- 创建/打开/保存/卸载场景 → `scene-create` / `scene-open` / `scene-save` / `scene-unload`

**脚本**

- 创建或更新脚本 → `script-update`（优先于直接写文件）
- 读取脚本 → `script-read`
- 动态编译执行（Roslyn）→ `code-run`

**反射**

- 查找任意 C# 方法 → `reflection-method-find`
- 调用任意 C# 方法 → `reflection-method-call`

**包管理**

- 安装/卸载包 → `package-install` / `package-remove`
- 搜索 Registry → `package-search`

**编辑器控制**

- 查询编辑器状态 → `editor-application-get-state`
- 控制 Play Mode → `editor-application-set-state`
- 读取 Console 日志 → `logs-get`

### MCP 不支持的操作 → 停止并告知用户

遇到以下操作时，停止执行，告知用户在 Editor 中手动完成，不尝试裸生成：

- ShaderGraph 编辑
- Animator Controller 配置
- Timeline 编辑
- OpenUPM 来源的包安装（MCP 仅支持 Registry / Git / Local）

---

## Boundaries

- 旧框架（`$PATH_OLD_FRAMEWORK`）只读，不修改任何文件
- 旧消费方项目（`$PATH_OLD_CONSUMER`）只读，不修改任何文件
- 不引入规范外的第三方库；需要新依赖时先确认再安装
- 禁止原生 `System.Threading.Task`
- 禁止裸 Subscribe（无生命周期绑定）
- 严禁 `FindObjectOfType`和`GetComponent`
- 禁止Unity原生生命周期方法，采用DI框架的`Inject`和生命周期管理

### Unity 资产边界

- 严禁裸生成 `.meta` 文件——GUID 必须由 Unity Editor 通过 MCP 自动生成
- 严禁裸生成 `.prefab` / `.unity` / `.asset` 文件
- `.cs` 文件优先通过 `script-update` 创建；直接写文件后必须调用 `assets-refresh`
- `.asmdef` 优先通过 Editor 创建；如裸生成，完成后必须调用 `assets-refresh` 并提示用户确认编译通过
- 任何 Unity 资产操作前，先查 Unity MCP 工具列表；无对应工具时停止并告知用户

---

## Workflow

开始任何模块开发前：

- 读 `$PATH_WORK_VAULT`/workflow/agent_workflow.md`
- 如果对应模块有规范，读 `$PATH_WORK_VAULT`/specs/<模块名>.md`

实现时如需参考项目结构或旧实现：

- 在 `$PATH_OLD_FRAMEWORK` 中查找对应模块（只读）

如需理解现有用法：

- 在 `$PATH_OLD_CONSUMER` 中查找调用示例（只读）

如果 unity-mcp 有版本更新：

- 检查 ReflectorNet PR #77 是否已合并
  （https://github.com/IvanMurzak/ReflectorNet/pull/77）
- 已合并则更新 unity-mcp，并移除 AGENTS.md 中的 schema 兼容性限制节

提交代码前：使用 git-commit skill
完成模块实现前：使用 self-review skill
