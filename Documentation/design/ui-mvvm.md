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

## 已知与设计意图的偏差

- **`ListPresenterBase.Reset` 静默丢弃创建参数**：应为当 ViewModel 需要 `TParam` 时，Reset 路径也能传递参数；当前默认实现调用无参 `CreateView`，对使用 `InitializableViewPresenterBase` 的子类，Reset 事件会绕过初始化逻辑而不报错。
