using System;
using System.Collections.Generic;
using System.Reflection;
using Ruka.Core.Symbols;
using UnityEditor;
using UnityEngine;

namespace Ruka.Editor.Symbols
{
    [CustomPropertyDrawer(typeof(SymbolSelectorAttribute))]
    public sealed class SymbolSelectorDrawer : PropertyDrawer
    {
        private const string ValueFieldName = "value";

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var attribute = (SymbolSelectorAttribute)this.attribute;

            if (!TryGetSymbolType(property, out var symbolType))
            {
                EditorGUI.PropertyField(position, property, label, true);
                return;
            }

            var candidates = SymbolCache.GetSymbols(symbolType, attribute.TargetGroups);
            DrawPopup(position, property, label, candidates, attribute.AllowManualInput);
        }

        private static void DrawPopup(
            Rect position,
            SerializedProperty property,
            GUIContent label,
            IReadOnlyList<SymbolEntry> candidates,
            bool allowManualInput)
        {
            var valueProperty = property.FindPropertyRelative(ValueFieldName);
            if (valueProperty == null)
            {
                EditorGUI.PropertyField(position, property, label, true);
                return;
            }

            var currentValue = valueProperty.stringValue ?? string.Empty;
            var displayOptions = new string[candidates.Count + 1];
            displayOptions[0] = "<None>";

            var selectedIndex = 0;
            for (var i = 0; i < candidates.Count; i++)
            {
                var entry = candidates[i];
                displayOptions[i + 1] = entry.DisplayName;
                if (string.Equals(entry.Value, currentValue, StringComparison.Ordinal))
                {
                    selectedIndex = i + 1;
                }
            }

            EditorGUI.BeginProperty(position, label, property);
            var popupRect = allowManualInput
                ? new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight)
                : position;

            var newIndex = EditorGUI.Popup(popupRect, label.text, selectedIndex, displayOptions);
            if (newIndex != selectedIndex)
            {
                valueProperty.stringValue = newIndex == 0 ? string.Empty : candidates[newIndex - 1].Value;
            }

            if (allowManualInput)
            {
                var fieldRect = new Rect(
                    position.x,
                    position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing,
                    position.width,
                    EditorGUIUtility.singleLineHeight);

                valueProperty.stringValue = EditorGUI.TextField(fieldRect, "Manual", valueProperty.stringValue);
            }

            EditorGUI.EndProperty();
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

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var attribute = (SymbolSelectorAttribute)this.attribute;
            if (!attribute.AllowManualInput)
            {
                return EditorGUIUtility.singleLineHeight;
            }

            return EditorGUIUtility.singleLineHeight * 2f + EditorGUIUtility.standardVerticalSpacing;
        }
    }

    internal readonly struct SymbolEntry
    {
        public SymbolEntry(string displayName, string value)
        {
            DisplayName = displayName;
            Value = value;
        }

        public string DisplayName { get; }
        public string Value { get; }
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
                return entriesByType.TryGetValue(symbolType, out var entries) ? entries : Array.Empty<SymbolEntry>();
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

        private static void EnsureCache()
        {
            if (initialized)
            {
                return;
            }

            entriesByType.Clear();
            groupedEntries.Clear();

            var providerType = typeof(SymbolProviderAttribute);
            var types = TypeCache.GetTypesWithAttribute(providerType);
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
            var fields = providerType.GetFields(BindingFlags.Public | BindingFlags.Static);
            foreach (var field in fields)
            {
                if (!field.FieldType.IsGenericType || field.FieldType.GetGenericTypeDefinition() != typeof(Symbol<>))
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

                var valueProperty = field.FieldType.GetProperty("Value", BindingFlags.Public | BindingFlags.Instance);
                var value = valueProperty?.GetValue(symbol) as string ?? string.Empty;
                yield return new SymbolEntry(field.Name, value);
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
                    fieldInfo = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
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
                    fieldInfo = type.GetField(element, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
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
