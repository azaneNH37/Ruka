using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Ruka.Utils.Core;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace Ruka.Editor.SType
{
    internal sealed class TypeDropdownItem : AdvancedDropdownItem
    {
        public TypeDropdownItem(string name, string assemblyQualifiedName, Texture2D icon) : base(name)
        {
            AssemblyQualifiedName = assemblyQualifiedName;
            this.icon = icon;
        }

        public string AssemblyQualifiedName { get; }
    }

    internal sealed class TypeSearchDropdown : AdvancedDropdown
    {
        private readonly Action<TypeDropdownItem> onSelected;
        private readonly IReadOnlyList<Type> types;
        private readonly Texture2D scriptIcon;

        public TypeSearchDropdown(
            AdvancedDropdownState state,
            IReadOnlyList<Type> types,
            Action<TypeDropdownItem> onSelected)
            : base(state)
        {
            this.types = types;
            this.onSelected = onSelected;
            scriptIcon = EditorGUIUtility.IconContent("FilterByLabel@2x").image as Texture2D;
            minimumSize = new Vector2(300f, 450f);
        }

        protected override AdvancedDropdownItem BuildRoot()
        {
            var root = new AdvancedDropdownItem("Select Type");
            root.AddChild(new TypeDropdownItem("<None>", string.Empty, null));
            root.AddSeparator();

            var groups = types
                .GroupBy(t => t.Namespace ?? "Global")
                .OrderBy(g => g.Key)
                .ToList();

            var showNamespaceLevel = groups.Count > 1;
            foreach (var group in groups)
            {
                var namespaceNode = root;
                if (showNamespaceLevel)
                {
                    namespaceNode = new AdvancedDropdownItem(group.Key)
                    {
                        icon = EditorGUIUtility.IconContent("Folder Icon").image as Texture2D
                    };
                    root.AddChild(namespaceNode);
                }

                foreach (var type in group.OrderBy(t => t.Name))
                {
                    var typeName = type.ReflectedType == null ? type.Name : $"{type.ReflectedType.Name}+{type.Name}";
                    var item = new TypeDropdownItem(typeName, type.AssemblyQualifiedName, scriptIcon);
                    namespaceNode.AddChild(item);
                }
            }

            return root;
        }

        protected override void ItemSelected(AdvancedDropdownItem item)
        {
            if (item is TypeDropdownItem typeItem)
            {
                onSelected?.Invoke(typeItem);
            }
        }
    }

    [CustomPropertyDrawer(typeof(SerializableType))]
    public sealed class SerializableTypeDrawer : PropertyDrawer
    {
        private const string TypeFieldName = "assemblyQualifiedName";
        private readonly AdvancedDropdownState dropdownState = new();
        private List<Type> cachedTypes;
        private Texture2D scriptIcon;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EnsureResources(property);

            var typeProperty = property.FindPropertyRelative(TypeFieldName);
            if (typeProperty == null)
            {
                EditorGUI.PropertyField(position, property, label, true);
                return;
            }

            var currentTypeName = typeProperty.stringValue;
            var buttonContent = BuildButtonContent(currentTypeName);

            position = EditorGUI.PrefixLabel(position, label);
            if (EditorGUI.DropdownButton(position, buttonContent, FocusType.Keyboard, EditorStyles.objectField))
            {
                var dropdown = new TypeSearchDropdown(
                    dropdownState,
                    cachedTypes,
                    item =>
                    {
                        typeProperty.stringValue = item.AssemblyQualifiedName;
                        typeProperty.serializedObject.ApplyModifiedProperties();
                    });

                dropdown.Show(position);
            }
        }

        private void EnsureResources(SerializedProperty property)
        {
            if (scriptIcon == null)
            {
                scriptIcon = EditorGUIUtility.IconContent("FilterByLabel@2x").image as Texture2D;
            }

            if (cachedTypes != null)
            {
                return;
            }

            var filterAttribute = fieldInfo.GetCustomAttribute<TypeFilterAttribute>();
            cachedTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(assembly => assembly.GetTypes())
                .Where(type => IsTypeAllowed(type, filterAttribute))
                .ToList();
        }

        private static GUIContent BuildButtonContent(string currentTypeName)
        {
            var content = new GUIContent("<None>");
            if (string.IsNullOrEmpty(currentTypeName))
            {
                return content;
            }

            var type = Type.GetType(currentTypeName);
            if (type == null)
            {
                content.text = "Missing Type";
                content.image = EditorGUIUtility.IconContent("console.erroricon.sml").image;
                return content;
            }

            content.text = type.ReflectedType == null ? type.Name : $"{type.ReflectedType.Name}+{type.Name}";
            content.image = EditorGUIUtility.IconContent("FilterByLabel@2x").image;
            return content;
        }

        private static bool IsTypeAllowed(Type type, TypeFilterAttribute filterAttribute)
        {
            if (type.IsGenericType)
            {
                return false;
            }

            if (filterAttribute == null)
            {
                return !type.IsAbstract && !type.IsInterface;
            }

            if (!MatchesKind(type, filterAttribute.FilterFlag))
            {
                return false;
            }

            var filterType = filterAttribute.FilterType;
            if (filterType == null)
            {
                return true;
            }

            return filterType.IsAssignableFrom(type);
        }

        private static bool MatchesKind(Type type, TypeFilterFlag flag)
        {
            var matchClass = (flag & TypeFilterFlag.Class) != 0 && type.IsClass && !type.IsAbstract;
            var matchInterface = (flag & TypeFilterFlag.Interface) != 0 && type.IsInterface;
            return matchClass || matchInterface;
        }
    }
}
