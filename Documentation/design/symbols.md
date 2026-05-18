# Symbols — 设计哲学

## 设计立场

### 枚举 / 裸字符串 的选择

枚举提供编译期穷举，但无法跨程序集扩展，不能序列化为可读字符串，且无法在 Inspector 中以下拉列表选择运行时动态添加的值。裸字符串无类型约束，不同域（窗口 ID、状态 ID、资产引用）的字符串可以互相混用而编译器不报警。

### `Symbol<T>` 的立场

以标记 struct 做类型参数，将字符串值限定在特定域内：`Symbol<WindowId>` 和 `Symbol<FsmId>` 是不同类型，编译器拒绝混用。底层仍是字符串，保留可读性、跨程序集扩展性和 Unity 序列化兼容性。明确背离了枚举的穷举语义——valid value set 由 `[SymbolProvider]` 类的 `public static` 字段定义，允许在任意程序集中扩展，不存在中心注册表。

## 核心取舍

| 特性 | 说明 |
|---|---|
| 编译期域隔离 | `Symbol<WindowId>` 与 `Symbol<FsmId>` 是不同类型，不可隐式互转 |
| 跨程序集扩展 | 任意程序集均可声明新的 `[SymbolProvider]` 类，无需修改中心定义 |
| Unity 序列化兼容 | 底层 `[SerializeField] string` 直接序列化，无 custom serializer |
| Inspector 可视化 | `[SymbolSelector]` 自动渲染 `AdvancedDropdown`，未定义值橙色高亮 |
| 值类型，零堆分配 | struct 使用不产生装箱，适合高频比较场景 |

| 代价 | 说明 |
|---|---|
| 无穷举能力 | 无法像 `Enum.GetValues` 一样获取所有有效值（运行时不可见） |
| Editor 缓存不自动刷新 | 新增 Provider 需域重载；Editor 运行期间下拉列表为快照 |
| 无编译期 exhaustiveness | `switch` 无法穷举所有可能值 |
| 标记 struct 有模板成本 | 每个新域需要定义一个空 struct |

## 已确认的设计意图

- **`Inherited = false` 在 `SymbolProviderAttribute`**: 是有意为之，原因：防止继承链上的子类意外继承父类的常量集合，造成下拉列表重复或误判。Provider 的常量集必须精确对应被标注类自身声明的字段。
- **相等性先比 hash 再比字符串**: 是有意为之，原因：旧框架（`Ruka-Unity-Foundation`）仅比较 hash，存在 hash 碰撞误判相等的风险。新框架以字符串 `Ordinal` 比较为最终判定，hash 仅作快速排除用。
- **`SymbolCache` 仅初始化一次**: 是有意为之，原因：`TypeCache` 查询成本低但每次 `OnGUI` 调用都触发会造成编辑器卡顿；一次性构建后在整个 Editor 会话中复用，更新路径为域重载（与脚本编译对齐）。
- **`TargetGroups` 为空时展示所有组**: 是有意为之，原因：无过滤意图时应展示全部可用常量；空数组等于"不过滤"而非"不展示"。

## 已知与设计意图的偏差

- **`SymbolCache` 跨 Editor PlayMode 不失效**: 应为 Editor PlayMode 进入/退出时重置缓存（防止 Play 期间动态注册的类型影响快照），当前为 Editor 会话全程共享同一份缓存，进入 PlayMode 不触发刷新。
