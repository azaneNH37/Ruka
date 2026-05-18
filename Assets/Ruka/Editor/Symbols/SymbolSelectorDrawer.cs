using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Ruka.Core.Symbols;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace Ruka.Editor.Symbols
{
    internal sealed class SymbolDropdownItem : AdvancedDropdownItem
    {
        public string SymbolValue { get; }

        public SymbolDropdownItem(string name, string value, Texture2D icon) : base(name)
        {
            SymbolValue = value;
            this.icon = icon;
        }
    }

    internal sealed class SymbolSearchDropdown : AdvancedDropdown
    {
        private readonly Action<string> onSelected;
        private readonly Dictionary<string, List<SymbolEntry>> data;
        private readonly Texture2D optionIcon;
        private readonly Texture2D folderIcon;
        private readonly Texture2D groupIcon;

        public SymbolSearchDropdown(
            AdvancedDropdownState state,
            Dictionary<string, List<SymbolEntry>> data,
            Action<string> onSelected)
            : base(state)
        {
            this.data = data;
            this.onSelected = onSelected;
            optionIcon = EditorGUIUtility.IconContent("FilterByLabel@2x").image as Texture2D;
            folderIcon = EditorGUIUtility.IconContent("Folder Icon").image as Texture2D;
            groupIcon = EditorGUIUtility.IconContent("SortingGroup Icon").image as Texture2D;
            minimumSize = new Vector2(300f, 450f);
        }

        protected override AdvancedDropdownItem BuildRoot()
        {
            var root = new AdvancedDropdownItem("Symbols");
            root.AddChild(new SymbolDropdownItem("<None>", string.Empty, null));
            root.AddSeparator();

            var showGroupLevel = data.Count > 1;

            foreach (var groupKv in data.OrderBy(x => x.Key))
            {
                var entries = groupKv.Value;

                AdvancedDropdownItem groupParent = root;
                if (showGroupLevel)
                {
                    groupParent = new AdvancedDropdownItem(groupKv.Key) { icon = groupIcon };
                    root.AddChild(groupParent);
                }

                var nsGroups = entries.GroupBy(e => e.Namespace).OrderBy(g => g.Key);
                var showNamespaceLevel = nsGroups.Count() > 1;
                foreach (var nsGroup in nsGroups)
                {
                    var nsNode = groupParent;
                    if (showNamespaceLevel)
                    {
                        nsNode = new AdvancedDropdownItem(nsGroup.Key) { icon = folderIcon };
                        groupParent.AddChild(nsNode);
                    }

                    foreach (var entry in nsGroup.OrderBy(e => e.Value))
                    {
                        nsNode.AddChild(new SymbolDropdownItem(entry.Value, entry.Value, optionIcon));
                    }
                }
            }

            return root;
        }

        protected override void ItemSelected(AdvancedDropdownItem item)
        {
            if (item is SymbolDropdownItem sItem)
            {
                onSelected?.Invoke(sItem.SymbolValue);
            }
        }
    }

    internal sealed class ManualInputPopup : PopupWindowContent
    {
        private readonly SerializedProperty valueProperty;
        private string tempValue;
        private bool shouldFocus = true;

        public ManualInputPopup(SerializedProperty valueProperty)
        {
            this.valueProperty = valueProperty;
            tempValue = valueProperty.stringValue;
        }

        public override Vector2 GetWindowSize() => new Vector2(200f, 50f);

        public override void OnGUI(Rect rect)
        {
            GUI.SetNextControlName("ManualInputField");
            tempValue = EditorGUILayout.TextField(tempValue);

            if (shouldFocus)
            {
                EditorGUI.FocusTextInControl("ManualInputField");
                shouldFocus = false;
            }

            GUILayout.Space(5);
            if (GUILayout.Button("Apply") || (Event.current.isKey && Event.current.keyCode == KeyCode.Return))
            {
                valueProperty.stringValue = tempValue;
                valueProperty.serializedObject.ApplyModifiedProperties();
                editorWindow.Close();
            }
        }
    }

    /// <summary>Property drawer for <see cref="Symbol{T}"/> fields marked with <see cref="SymbolSelectorAttribute"/>. Auto-applied by Unity; do not reference directly.</summary>
    [CustomPropertyDrawer(typeof(SymbolSelectorAttribute))]
    public sealed class SymbolSelectorDrawer : PropertyDrawer
    {
        private const string ValueFieldName = "value";
        private static readonly Color MissingSymbolColor = new Color(1f, 0.7f, 0.2f);

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var attribute = (SymbolSelectorAttribute)this.attribute;

            if (!TryGetSymbolType(property, out var symbolType))
            {
                EditorGUI.PropertyField(position, property, label, true);
                return;
            }

            var valueProperty = property.FindPropertyRelative(ValueFieldName);
            if (valueProperty == null)
            {
                EditorGUI.PropertyField(position, property, label, true);
                return;
            }

            var currentValue = valueProperty.stringValue ?? string.Empty;
            var options = SymbolCache.GetGroupedSymbols(symbolType, attribute.TargetGroups);
            var isDefined = string.IsNullOrEmpty(currentValue) || IsSymbolInOptions(options, currentValue);

            position = EditorGUI.PrefixLabel(position, label);

            var mainBtnRect = position;
            var editBtnRect = Rect.zero;

            if (attribute.AllowManualInput)
            {
                mainBtnRect.width -= 20f;
                editBtnRect = new Rect(position.x + mainBtnRect.width + 2f, position.y, 20f, position.height);
            }

            var displayLabel = string.IsNullOrEmpty(currentValue) ? "<None>" : currentValue;
            var icon = (Texture2D)EditorGUIUtility.IconContent(isDefined ? "FilterByLabel@2x" : "DefaultAsset Icon").image;

            var originalColor = GUI.contentColor;
            if (!isDefined)
            {
                GUI.contentColor = MissingSymbolColor;
            }

            if (EditorGUI.DropdownButton(mainBtnRect, new GUIContent(displayLabel, icon), FocusType.Keyboard, EditorStyles.objectField))
            {
                ShowDropdown(mainBtnRect, symbolType, attribute.TargetGroups, valueProperty);
            }

            GUI.contentColor = originalColor;

            if (attribute.AllowManualInput)
            {
                var editIcon = EditorGUIUtility.IconContent("CreateAddNew@2x");
                editIcon.tooltip = "Manual Input";

                if (GUI.Button(editBtnRect, editIcon, EditorStyles.iconButton))
                {
                    PopupWindow.Show(editBtnRect, new ManualInputPopup(valueProperty));
                }
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight;
        }

        private static void ShowDropdown(Rect rect, Type symbolType, string[] targetGroups, SerializedProperty valueProperty)
        {
            var options = SymbolCache.GetGroupedSymbols(symbolType, targetGroups);
            var dropdown = new SymbolSearchDropdown(new AdvancedDropdownState(), options, id =>
            {
                valueProperty.stringValue = id;
                valueProperty.serializedObject.ApplyModifiedProperties();
            });
            dropdown.Show(rect);
        }

        private static bool IsSymbolInOptions(Dictionary<string, List<SymbolEntry>> options, string id)
        {
            return options.Values.Any(group => group.Any(x => x.Value == id));
        }

        private static bool TryGetSymbolType(SerializedProperty property, out Type symbolType)
        {
            symbolType = null;

            if (property == null)
            {
                return false;
            }

            var fieldInfo = property.GetFieldInfoFromPropertyPath();
            if (fieldInfo == null)
            {
                return false;
            }

            var fieldType = fieldInfo.FieldType;
            if (!fieldType.IsGenericType || fieldType.GetGenericTypeDefinition() != typeof(Symbol<>))
            {
                return false;
            }

            symbolType = fieldType.GetGenericArguments()[0];
            return true;
        }
    }

    internal readonly struct SymbolEntry
    {
        public SymbolEntry(string displayName, string value, string ns = "")
        {
            DisplayName = displayName;
            Value = value;
            Namespace = ns ?? string.Empty;
        }

        public string DisplayName { get; }
        public string Value { get; }
        public string Namespace { get; }
    }

    internal static class SymbolCache
    {
        private static readonly Dictionary<Type, List<SymbolEntry>> entriesByType = new();
        private static readonly Dictionary<Type, Dictionary<string, List<SymbolEntry>>> groupedEntries = new();
        private static bool initialized;

        public static IReadOnlyList<SymbolEntry> GetSymbols(Type symbolType, string[] targetGroups)
        {
            EnsureCache();

            if (targetGroups == null || targetGroups.Length == 0)
            {
                return entriesByType.TryGetValue(symbolType, out var entries)
                    ? entries
                    : Array.Empty<SymbolEntry>();
            }

            var results = new List<SymbolEntry>();
            if (!groupedEntries.TryGetValue(symbolType, out var groups))
            {
                return results;
            }

            for (var i = 0; i < targetGroups.Length; i++)
            {
                var group = targetGroups[i];
                if (string.IsNullOrEmpty(group))
                {
                    continue;
                }

                if (groups.TryGetValue(group, out var groupEntries))
                {
                    results.AddRange(groupEntries);
                }
            }

            return results;
        }

        public static Dictionary<string, List<SymbolEntry>> GetGroupedSymbols(
            Type symbolType,
            string[] targetGroups)
        {
            EnsureCache();

            if (!groupedEntries.TryGetValue(symbolType, out var allGroups))
            {
                return new Dictionary<string, List<SymbolEntry>>();
            }

            if (targetGroups == null || targetGroups.Length == 0)
            {
                return allGroups;
            }

            var result = new Dictionary<string, List<SymbolEntry>>(StringComparer.Ordinal);
            for (var i = 0; i < targetGroups.Length; i++)
            {
                var group = targetGroups[i];
                if (!string.IsNullOrEmpty(group) && allGroups.TryGetValue(group, out var entries))
                {
                    result[group] = entries;
                }
            }

            return result;
        }

        private static void EnsureCache()
        {
            if (initialized)
            {
                return;
            }

            entriesByType.Clear();
            groupedEntries.Clear();

            var types = TypeCache.GetTypesWithAttribute<SymbolProviderAttribute>();
            foreach (var type in types)
            {
                var providerAttributes = type.GetCustomAttributes<SymbolProviderAttribute>();
                foreach (var provider in providerAttributes)
                {
                    if (provider.SymbolType == null)
                    {
                        continue;
                    }

                    var group = provider.GroupName ?? string.Empty;
                    var symbols = GetSymbolsFromProvider(type, provider.SymbolType);
                    AddEntries(provider.SymbolType, group, symbols);
                }
            }

            initialized = true;
        }

        private static IEnumerable<SymbolEntry> GetSymbolsFromProvider(Type providerType, Type symbolType)
        {
            var providerNamespace = providerType.Namespace ?? string.Empty;
            var fields = providerType.GetFields(BindingFlags.Public | BindingFlags.Static);

            foreach (var field in fields)
            {
                if (!field.FieldType.IsGenericType
                    || field.FieldType.GetGenericTypeDefinition() != typeof(Symbol<>))
                {
                    continue;
                }

                var fieldSymbolType = field.FieldType.GetGenericArguments()[0];
                if (fieldSymbolType != symbolType)
                {
                    continue;
                }

                var symbol = field.GetValue(null);
                if (symbol == null)
                {
                    continue;
                }

                var valueProperty = field.FieldType.GetProperty("Value",
                    BindingFlags.Public | BindingFlags.Instance);
                var value = valueProperty?.GetValue(symbol) as string ?? string.Empty;
                yield return new SymbolEntry(field.Name, value, providerNamespace);
            }
        }

        private static void AddEntries(Type symbolType, string group, IEnumerable<SymbolEntry> entries)
        {
            if (!entriesByType.TryGetValue(symbolType, out var allEntries))
            {
                allEntries = new List<SymbolEntry>();
                entriesByType[symbolType] = allEntries;
            }

            if (!groupedEntries.TryGetValue(symbolType, out var groups))
            {
                groups = new Dictionary<string, List<SymbolEntry>>(StringComparer.Ordinal);
                groupedEntries[symbolType] = groups;
            }

            if (!groups.TryGetValue(group, out var groupEntries))
            {
                groupEntries = new List<SymbolEntry>();
                groups[group] = groupEntries;
            }

            foreach (var entry in entries)
            {
                allEntries.Add(entry);
                groupEntries.Add(entry);
            }
        }
    }

    internal static class SerializedPropertyExtensions
    {
        public static FieldInfo GetFieldInfoFromPropertyPath(this SerializedProperty property)
        {
            if (property == null)
            {
                return null;
            }

            var target = property.serializedObject.targetObject;
            var type = target.GetType();
            var path = property.propertyPath;

            FieldInfo fieldInfo = null;
            var elements = path.Replace(".Array.data[", "[").Split('.');
            for (var i = 0; i < elements.Length; i++)
            {
                var element = elements[i];
                if (element.Contains("["))
                {
                    var fieldName = element.Substring(0, element.IndexOf("[", StringComparison.Ordinal));
                    fieldInfo = type.GetField(fieldName,
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (fieldInfo == null)
                    {
                        return null;
                    }

                    type = fieldInfo.FieldType;
                    if (type.IsArray)
                    {
                        type = type.GetElementType();
                    }
                    else if (type.IsGenericType)
                    {
                        type = type.GetGenericArguments()[0];
                    }
                }
                else
                {
                    fieldInfo = type.GetField(element,
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (fieldInfo == null)
                    {
                        return null;
                    }

                    type = fieldInfo.FieldType;
                }
            }

            return fieldInfo;
        }
    }
}
