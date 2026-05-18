# UI.MVVM — 设计哲学

## 设计立场

### 传统 MVVM 框架的做法

传统 MVVM 框架（WPF、ReactiveUI 等）通常通过数据绑定引擎实现 View ↔ ViewModel 的自动同步，ViewModel 不感知 View 类型，View 通过 Binding 表达式声明依赖。

### 本模块的立场

放弃自动绑定引擎，采用显式 `Bind(TViewModel)` 方法由 View 自行订阅 ViewModel 的 R3 属性。这将 Unity Inspector 序列化工作流与 MVVM 的生命周期管理分离——View 由 Unity 负责创建，ViewModel 由 DI 容器负责创建，Presenter 负责配对并绑定。明确背离了"View 不应感知 ViewModel 类型"的原则，转而接受强类型 `IView<TViewModel>` 合约，以换取完整的编译期类型安全和 IDE 支持。

### `ViewFactory` 抽象的取舍

旧框架（`Ruka-Unity-Foundation` / `Ruka.Core.MVVM`）将 `prefab + parent + resolver` 封装为 `ViewFactory<TViewModel, TView>` 对象，作为 Presenter 构造函数的单一参数。当前框架直接将三个参数展开传入 Presenter，消除了一层中间抽象。代价是消费方在注册时必须显式调用 `.WithParameter(prefab).WithParameter(parent)`，无法将工厂对象作为独立 ScriptableObject 配置。取舍方向：减少间接层，优先让 DI 容器直接管理依赖关系。

## 核心取舍

| 特性 | 说明 |
|---|---|
| 强类型 View-ViewModel 配对 | `IView<TViewModel>` 在编译期绑定 View 和 ViewModel 类型，不依赖反射或字符串匹配 |
| 统一生命周期管理 | `CreateView`/`RemoveView` 封装 Instantiate + Resolve + Bind + Destroy + Dispose 为原子操作 |
| 列表增量同步 | `ListPresenterBase` 复用 `ObservableCollections` 的增量协议，不重新定义 delta 语义 |
| 无额外中间层 | Presenter 直接持有 prefab 和 parent，不需要 ViewFactory 工厂对象 |

| 代价 | 说明 |
|---|---|
| 无对象池 | 每次 `CreateView` 均 `Instantiate` + `Resolve`；高频创建/销毁场景有 GC 压力 |
| 无编译期 `IInitializableViewModel` 检查 | `CreateView<TParam>` 使用运行时 `is` 检查，类型不匹配在运行时才报错 |
| prefab/parent 必须 `.WithParameter()` | 这两者不在 DI 容器中注册为独立类型，遗忘传参会导致运行时注入失败 |
| Move 为 no-op | 若业务需要响应列表重排，必须覆写 `ApplyDelta`，默认实现不处理 |

## 已确认的设计意图

- **`CreateView` 隐式替换旧实例**: 是有意为之，原因：对同一 id 的重复创建视为语义上的"替换"而非错误，避免调用方先 `RemoveView` 再 `CreateView` 的样板代码。
- **`Move` case 为 no-op**: 是有意为之，原因：UI 中的顺序通常由布局组件（如 `VerticalLayoutGroup`）控制，View GameObject 的 sibling index 并不等于数据模型中的顺序；移动不应触发销毁重建，以免播放错误的创建动画。
- **`IInitializableViewModel<TParam>` 为运行时可选接口**: 是有意为之，原因：若在 Presenter 的泛型约束上强制 `where TViewModel : IInitializableViewModel<TParam>`，则无参构造的 ViewModel 和有参构造的 ViewModel 必须是两个不同的 Presenter 基类。当前设计在一个基类中同时支持两种创建路径，牺牲了编译期安全换取了 API 面的统一。
- **ViewModel 不感知 View 类型**: 是有意为之，原因：`IViewModel` 不持有任何对 View 的引用，ViewModel 的可观察属性对 View 是推送关系，允许在不修改 ViewModel 的情况下替换 View 实现。

## 已知与设计意图的偏差

- **`Models` 和 `Views` 字典对子类完全可写**: 应为只读暴露（如 `IReadOnlyDictionary`），当前为 `protected readonly Dictionary`（`readonly` 约束 field reference，不约束内容），子类可绕过 `CreateView`/`RemoveView` 直接增删条目，破坏 View-ViewModel 一致性。
