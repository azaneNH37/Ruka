# Module: Core.Symbols

## 职责

通过泛型结构体 `Symbol<T>` 将字符串 ID 与标记 struct 类型绑定，在编译期区分不同域的 ID；通过 `SymbolProviderAttribute` 与 `SymbolSelectorAttribute` 配合编辑器缓存，使 Inspector 中的 `Symbol<T>` 字段能以下拉列表选择已声明的常量，并以橙色高亮未被任何 Provider 注册的值。

## 非职责

- 不提供运行时动态注册或删除 symbol 的能力
- 不保证 symbol 在不同进程或持久化存储中的全局唯一性（以字符串值为主键，不以 hash 为主键）
- 不提供枚举语义（无穷举能力、`switch` exhaustiveness、`Enum.GetValues` 等）
- 不校验 Inspector 中填写的值是否存在于任何 Provider（仅视觉警告，不阻止保存）

## 公开 API

```csharp
namespace Ruka.Core.Symbols
{
    /// <summary>
    /// Type-safe string ID parameterized by a marker struct. Not a replacement for enum — use when IDs
    /// must be serializable, Inspector-selectable, and scoped to a specific domain type.
    /// </summary>
    [Serializable]
    public struct Symbol<T> : IEquatable<Symbol<T>>, ISerializationCallbackReceiver where T : struct
    {
        /// <summary>Constructs a Symbol wrapping the given string value.</summary>
        public Symbol(string value);

        /// <summary>The underlying string value. Returns empty string when unset or null.</summary>
        public string Value { get; }

        /// <summary>Lazily computed FNV-1a hash of Value. Always reflects the current Value; recomputed after Unity deserialization.</summary>
        public int Hash { get; }

        /// <summary>True when Value is null or empty.</summary>
        public bool IsEmpty { get; }

        public bool Equals(Symbol<T> other);
        public static bool operator ==(Symbol<T> left, Symbol<T> right);
        public static bool operator !=(Symbol<T> left, Symbol<T> right);
        public static implicit operator string(Symbol<T> symbol);
    }

    /// <summary>
    /// Marks a static class as a source of Symbol{T} constants for editor dropdown discovery.
    /// Apply once per domain-group pair; a single class may carry multiple instances for different types or groups.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class SymbolProviderAttribute : Attribute
    {
        /// <summary>Declares this class as a provider of constants for symbolType within the named group.</summary>
        /// <param name="symbolType">The marker struct type (the T in Symbol{T}) that this class provides constants for.</param>
        /// <param name="groupName">Group label shown in the dropdown hierarchy.</param>
        public SymbolProviderAttribute(Type symbolType, string groupName);

        public Type SymbolType { get; }
        public string GroupName { get; }
    }

    /// <summary>
    /// Marks a [SerializeField] Symbol{T} field to render as a searchable dropdown populated by SymbolProviderAttribute classes.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = true)]
    public sealed class SymbolSelectorAttribute : PropertyAttribute
    {
        /// <param name="targetGroups">Groups to include. Empty means all registered groups for the symbol type.</param>
        public SymbolSelectorAttribute(params string[] targetGroups);

        /// <summary>Groups to include in the dropdown. Empty means all registered groups for the symbol type.</summary>
        public string[] TargetGroups { get; }

        /// <summary>Enables freeform text input alongside the dropdown. Disable when the valid value set is closed (all values come from providers).</summary>
        public bool AllowManualInput { get; set; }
    }
}
```

## 最小使用示例

```csharp
// 1. Define a marker struct for the domain — no members needed
public struct WindowId { }

// 2. Declare constants in a static class; [SymbolProvider] registers them for editor discovery
[SymbolProvider(typeof(WindowId), "Windows")]
public static class WindowIds
{
    public static readonly Symbol<WindowId> MainMenu = new("MainMenu");
    public static readonly Symbol<WindowId> Inventory = new("Inventory");
}

// 3. Consume in a serialized component — [SymbolSelector] renders the dropdown
public class WindowOpener : MonoBehaviour
{
    // TargetGroups omitted: shows all groups for WindowId
    [SerializeField, SymbolSelector]
    private Symbol<WindowId> targetWindow;

    private IWindowService _windowService;

    [Inject]
    private void Construct(IWindowService windowService) => _windowService = windowService;

    public async UniTask OpenAsync(CancellationToken ct)
    {
        await _windowService.OpenWindowAsync(targetWindow, ct);
    }
}

// 4. Use Symbol<T> as a dictionary key or comparison target
bool isSame = someSymbol == WindowIds.MainMenu;
bool isEmpty = someSymbol.IsEmpty;
```

## 关键设计约束

- **`SymbolCache` 不在 Editor 运行期内重新初始化**: `SymbolCache` 在首次使用时通过 `TypeCache.GetTypesWithAttribute<SymbolProviderAttribute>()` 构建，并在整个 Editor 会话中保持不变。新增 `[SymbolProvider]` 类后，需触发域重载（脚本编译）才会出现在下拉列表中。
- **`SymbolProviderAttribute` 不可继承 (`Inherited = false`)**: 子类不继承父类的 `[SymbolProvider]` 注册；常量必须声明在被标注的那个类本身的 `public static` 字段上。
- **相等性以字符串为权威**: `Equals` 先比 hash 短路，再以 `StringComparison.Ordinal` 比较字符串。hash 相同但字符串不同（碰撞）时判为不等。不应依赖 hash 值本身做持久化存储。
- **`ISerializationCallbackReceiver` 用于 hash 一致性**: `OnAfterDeserialize` 仅重置 `hashInitialized`，确保 hash 始终从反序列化后的字符串值惰性重新计算，不会携带旧值。
- **标记 struct `T` 仅作类型参数**: `T` 在运行时不实例化，约束为 `struct` 以避免引用类型带来的额外语义。不要在 `T` 上添加任何成员；其唯一作用是在编译期区分不同域。

## 依赖

- `Ruka.Utils` — `StringExtensions.GetStableHash()` (FNV-1a hash 实现)
- `UnityEngine` — `SerializeField`, `ISerializationCallbackReceiver`, `PropertyAttribute`
- `UnityEditor` (Editor 仅用) — `PropertyDrawer`, `TypeCache`, `AdvancedDropdown`
