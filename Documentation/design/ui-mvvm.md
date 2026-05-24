# UI.MVVM — 设计哲学

## 设计立场

### 传统 MVVM 框架的做法

传统 MVVM 框架（WPF、ReactiveUI 等）通过数据绑定引擎实现 View ↔ ViewModel 的自动同步，ViewModel 不感知 View 类型，View 通过 Binding 表达式声明依赖。框架本身负责订阅管理和值传播。

### 本模块的立场

放弃自动绑定引擎，采用显式 `Bind(TViewModel)` 方法由 View 自行订阅 ViewModel 的 R3 属性。Unity 没有可用的声明式绑定基础设施（uGUI 不提供，UI Toolkit 绑定尚不成熟），因此手动 `Bind()` 是唯一不引入自研框架的方案。

明确背离了"View 不应感知 ViewModel 类型"的原则，转而接受强类型 `IView<TViewModel>` 合约，换取完整的编译期类型安全和 IDE 支持。整体模式更接近 MVP（Presenter 主动管理 View 和 ViewModel 的配对与生命周期）而非经典 MVVM（View 被动绑定）。

### `ViewFactory` 抽象的取舍

旧框架（`Ruka-Unity-Foundation`）将 `prefab + parent + resolver` 封装为 `ViewFactory<TViewModel, TView>` 对象，作为 Presenter 构造函数的单一参数。当前框架直接将三个参数展开传入 Presenter，并通过 `RegisterMVVM` 扩展将注册步骤封装为类型安全的单次调用。消除了工厂中间层，代价是 prefab/parent 不能作为独立 ScriptableObject 配置。

## 核心取舍

| 特性 | 说明 |
|---|---|
| 强类型 View-ViewModel 配对 | `IView<TViewModel>` 在编译期绑定类型，不依赖反射或字符串匹配 |
| 统一生命周期管理 | `CreateView`/`RemoveView` 封装 Acquire + Resolve + Bind + Release + Dispose 为原子操作 |
| 列表增量同步 | `ListPresenterBase` 复用 `ObservableCollections` 增量协议，不重新定义 delta 语义 |
| 注册类型安全 | `RegisterMVVM` 的泛型约束在编译期验证 Presenter、ViewModel、View 三者的兼容性 |
| 池化扩展点内置 | `AcquireView`/`ReleaseView` 虚方法允许子类接入对象池，不修改基类逻辑 |

| 代价 | 说明 |
|---|---|
| 无内置对象池 | 默认 `AcquireView` 调用 `Instantiate`；虚拟列表等高频场景需配合 Ruka.Pool 覆写 |
| `ListPresenterBase.Reset` 不传递创建参数 | Reset 处理路径调用无参 `CreateView`，对需要 `TParam` 的 ViewModel 存在静默行为退化 |
| `Move` 为 no-op | 响应列表重排需覆写 `ApplyDelta`，默认实现不处理 |
| `RegisterMVVM` 假设 ViewModel 独占 | 共用 ViewModel 或非 Transient 生命周期的场景需要手动注册 |

## 已确认的设计意图

- **`CreateView` 隐式替换旧实例**：是有意为之，原因：对同一 id 的重复创建视为语义上的"替换"，避免调用方先 `RemoveView` 再 `CreateView` 的样板代码。
- **`Move` case 为 no-op**：是有意为之，原因：UI 中的顺序通常由布局组件（如 `VerticalLayoutGroup`）控制，View GameObject 的 sibling index 并不等于数据顺序；移动不应触发销毁重建，以免播放错误的创建动画。
- **`IInitializableViewModel<TParam>` 在 `ViewPresenterBase` 中为运行时可选检查**：是有意为之，原因：在基类中同时支持无参和有参两种创建路径保持 API 面统一；需要编译期保证的场景通过 `InitializableViewPresenterBase` 独立基类覆盖，不修改 `ViewPresenterBase` 的约束。
- **`ViewBase<TViewModel>` 为可选基类**：是有意为之，原因：有自定义 MonoBehaviour 基类（如带动画的 `AnimatedView`）的项目无法同时继承 `ViewBase`；非 rebind 场景不应被强制约束。`VirtualListPresenterBase` 通过 `TView : ViewBase<TViewModel>` 约束在需要时自然收紧。
- **`SingleViewPresenterBase` 以 `R3.Unit` 为内部 key**：是有意为之，原因：在 `ViewPresenterBase` 基础上复用全部生命周期逻辑，不引入新字段；`Unit` 的语义是"不需要区分的唯一项"，比 `0` 或 `string.Empty` 哨兵值更准确。
- **`EnableUpdate`/`OnUpdate` 被删除**：是有意为之，原因：per-frame pull 模式（Presenter 每帧向 ViewModel 推送数据）与响应式数据流矛盾——若 ViewModel 的状态正确地由注入服务的 Observable 驱动，不需要帧轮询。需要帧更新的逻辑层场景应在 `ITickable` 中处理。
- **`AcquireView`/`ReleaseView` 不出现在公开 API 规范中**：是有意为之，原因：这两个方法是框架内部扩展点，对象池集成由子类完成；将它们暴露为可直接调用的公开 API 会产生误用风险（消费方绕过 `CreateView`/`RemoveView` 手动操作槽位）。
- **`ViewModel 状态只由注入依赖驱动` 是规范约定而非 API 强制**：是有意为之，原因：禁止 Presenter 向 ViewModel ReactiveProperty 赋值在 C# 类型系统内无法完全强制（除非所有状态都是只读属性）；通过规范约定而非 API 限制来管理，保持了 ViewModel 暴露可读可写属性用于测试的灵活性。

## MVVM 链路边界

### 概念定位

```
Logic（纯 C#）
  ↓ 注入
ViewModel（纯 C#）
  ↓ Bind
View（MonoBehaviour）
  ↑ 生命周期管理
Presenter（纯 C#，IInitializable）
```

### Logic

- **定义**：业务规则、状态机、用例编排。典型形式为 `IFeatureInstaller` 注册的 Scoped 服务（如 `RoomService`、`PlayerSessionService`）。
- **允许**：持有 R3 Subject / Observable 作为内部或对外状态流；暴露 `UniTask` 异步方法供上层调用。
- **禁止**：感知 ViewModel 或 View 的存在；调用 `IWindowService` 或任何 UI 系统；直接操作 Unity 场景对象。
- **程序集归属**：`Nbr.Framework` 或 `Nbr.Gameplay`；不引用 `Ruka.UI`。

### ViewModel

- **定义**：将 Logic 层的业务状态转换为展示状态，以 R3 Observable 形式暴露；持有表示用户意图的异步命令（`UniTask` 方法）。实现 `IViewModel`（来自 `Ruka.Core.MVVM`）。
- **允许**：注入 Logic 服务并订阅其 Observable；对原始业务数据做展示级转换（格式化字符串、百分比、枚举→文案映射等）；暴露 `UniTask` 命令方法，内部委托给 Logic 服务执行。
- **禁止**：感知 View 类型或持有 View 引用；调用 `IWindowService` 直接开窗（应由 Presenter 或 View 层决策）；包含 Unity 渲染或布局逻辑；引用 FishNet 或任何网络框架类型。
- **程序集归属**：与其主要依赖的 Logic 服务同程序集（`Nbr.Framework` 或 `Nbr.Gameplay`）；只需引用 `Ruka.Core`，不引用 `Ruka.UI`。
- **生命周期**：场景级 ViewModel 注册为 `Scoped`，跟随 Scope 存活；列表项 ViewModel 注册为 `Transient`，由 `ViewPresenterBase` 在 `CreateView`/`RemoveView` 时创建和 Dispose。

### View

- **定义**：`MonoBehaviour`，在 `Bind(TViewModel)` 中建立对 ViewModel Observable 的订阅并渲染；在 Unity UI 事件回调中调用 ViewModel 的命令方法。实现 `IView<TViewModel>`（来自 `Ruka.UI.MVVM`）。
- **允许**：订阅 ViewModel 的 Observable；调用 ViewModel 的 `UniTask` 命令；操作 Unity UI 组件（`TMP_Text`、`Slider`、`Image` 等）；使用 LitMotion 驱动本地动画。
- **禁止**：持有 Logic 服务引用；直接修改业务状态；包含条件判断逻辑（"如果 Phase == Hosting 则…"应在 ViewModel 中转换为 Observable<bool>）；持有 ViewModel 之外的业务对象引用。
- **程序集归属**：`Nbr.UI`；引用 `Ruka.UI` + 对应的业务程序集（以获取 ViewModel 类型）。

### Presenter

- **定义**：继承 `ViewPresenterBase`（或其变体）的纯 C# 类，实现 `IInitializable`；负责在正确时机调用 `CreateView`/`RemoveView`，将 View 和 ViewModel 的生命周期作为一个原子单元管理。
- **允许**：注入 Logic 服务以决定何时创建/销毁 View-ViewModel 对；订阅 Logic 的 Observable 以响应列表变化（通过 `BindList`）；注入 `IWindowService` 以在合适时机开窗。
- **禁止**：向 ViewModel 的属性赋值（ViewModel 状态只由其注入依赖驱动）；在 Presenter 中实现业务规则；成为 Logic 和 ViewModel 之间的数据搬运中间人。
- **程序集归属**：`Nbr.UI`；它是 Logic 层与 View 层之间唯一合法的双向引用点。

### 依赖方向汇总

```
Nbr.UI (View, Presenter)
    → Nbr.Framework / Nbr.Gameplay (ViewModel, Logic)   ✅
    → Ruka.UI (ViewBase, ViewPresenterBase)              ✅

Nbr.Framework / Nbr.Gameplay (ViewModel, Logic)
    → Ruka.Core (IViewModel)                             ✅
    ✗ → Ruka.UI                                          ❌ 禁止
    ✗ → Nbr.UI                                           ❌ 禁止
```

### 常见违反模式

| 违反 | 正确做法 |
|------|---------|
| Logic 注入 `IWindowService` 直接开窗 | Logic 发出 Observable 事件，Presenter 订阅后调用 `IWindowService` |
| View 直接订阅 Logic 服务的 Observable | View 只订阅 ViewModel 暴露的 Observable |
| Presenter 向 ViewModel 字段赋值 | ViewModel 状态来自构造注入的 Logic 服务，Presenter 只控制时机 |
| ViewModel 持有 View 引用 | ViewModel 不感知 View，View 单向订阅 ViewModel |
| ViewModel 放入 `Nbr.UI` 程序集 | ViewModel 与其依赖的 Logic 同程序集（Framework/Gameplay） |

## 已知与设计意图的偏差

- **`ListPresenterBase.Reset` 静默丢弃创建参数**：应为当 ViewModel 需要 `TParam` 时，Reset 路径也能传递参数；当前默认实现调用无参 `CreateView`，对使用 `InitializableViewPresenterBase` 的子类，Reset 事件会绕过初始化逻辑而不报错。
