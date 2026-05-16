using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Ruka.Editor
{
    public class ScriptableObjectCreationWizard : EditorWindow
    {
        [MenuItem("Tools/ScriptableObject Creator Wizard %&G")]
        public static void Open()
        {
            var window = GetWindow<ScriptableObjectCreationWizard>("ScriptableObject Creator");
            window.titleContent = new GUIContent("ScriptableObject Creator", EditorGUIUtility.IconContent("ScriptableObject Icon").image);
            window.minSize = new Vector2(750, 600);
            window.RefreshTypes();
        }

        private class CategoryNode
        {
            public string Name;
            public Dictionary<string, CategoryNode> SubCategories = new Dictionary<string, CategoryNode>();
            public List<TypeData> Types = new List<TypeData>();
            public bool IsExpanded = true;
        }

        internal class TypeData
        {
            public Type Type;
            public string DisplayName;
            public string DefaultFileName;
            public string AssemblyName;
        }

        private CategoryNode _rootNode;
        private CategoryNode _selectedNode;
        private readonly List<TypeData> _searchResult = new List<TypeData>();
        private TypeData _selectedTypeData;

        private Vector2 _treeScroll;
        private Vector2 _listScroll;

        private string _searchText = string.Empty;
        private string _targetFolder = "Assets";
        private string _fileName = "NewAsset";
        private ScriptableObject _previewObject;
        private UnityEditor.Editor _previewEditor;

        private string FileNamePreview => $"{_targetFolder}/{_fileName}.asset";
        private bool IsValidPath => !string.IsNullOrEmpty(_targetFolder) && _targetFolder.StartsWith("Assets");
        private bool IsInvalidPath => !IsValidPath;
        private bool IsWarningPath => !string.IsNullOrEmpty(_targetFolder) && _targetFolder.Equals("Assets");

        private static readonly string[] ValidationIcons = { "TestPassed", "TestFailed", "console.warnicon" };
        private static readonly Color[] ValidationColors = { Color.green, Color.red, Color.yellow };
        private static readonly string[] ValidationMessages =
        {
            "Valid Path",
            "Invalid Path (outside of folder Assets)",
            "Path Discouraged (e.g. Assets)"
        };

        private void OnEnable()
        {
            RefreshTypes();
            UpdateSelectedPath();
        }

        private void OnFocus() => UpdateSelectedPath();

        private void OnDestroy()
        {
            if (_previewObject != null) DestroyImmediate(_previewObject);
            if (_previewEditor != null) DestroyImmediate(_previewEditor);
        }

        private void RefreshTypes()
        {
            _rootNode = new CategoryNode { Name = "Root" };
            var types = TypeCache.GetTypesWithAttribute<CreateAssetMenuAttribute>();

            foreach (var type in types)
            {
                var attr = (CreateAssetMenuAttribute)Attribute.GetCustomAttribute(type, typeof(CreateAssetMenuAttribute));
                var menuName = string.IsNullOrEmpty(attr.menuName) ? type.Name : attr.menuName;
                AddTypeToTree(type, menuName, attr.fileName);
            }

            OnSearchChanged();
        }

        private void AddTypeToTree(Type type, string menuPath, string defaultFileName)
        {
            string[] parts = menuPath.Split('/');
            CategoryNode currentNode = _rootNode;

            for (int i = 0; i < parts.Length - 1; i++)
            {
                string part = parts[i];
                if (!currentNode.SubCategories.ContainsKey(part))
                    currentNode.SubCategories[part] = new CategoryNode { Name = part };
                currentNode = currentNode.SubCategories[part];
            }

            currentNode.Types.Add(new TypeData
            {
                Type = type,
                DisplayName = parts.Last(),
                DefaultFileName = defaultFileName,
                AssemblyName = type.Assembly.GetName().Name
            });
        }

        private void OnSearchChanged()
        {
            _searchResult.Clear();
            if (!string.IsNullOrWhiteSpace(_searchText))
                FindTypesRecursive(_rootNode, _searchText.ToLower(), _searchResult);
        }

        private void FindTypesRecursive(CategoryNode node, string query, List<TypeData> results)
        {
            foreach (var t in node.Types)
                if (t.DisplayName.ToLower().Contains(query))
                    results.Add(t);

            foreach (var sub in node.SubCategories.Values)
                FindTypesRecursive(sub, query, results);
        }

        private void UpdateSelectedPath()
        {
            string path = "Assets";
            var selected = Selection.GetFiltered(typeof(UnityEngine.Object), SelectionMode.Assets);

            if (selected.Length > 0)
            {
                path = AssetDatabase.GetAssetPath(selected[0]);
                if (File.Exists(path))
                    path = Path.GetDirectoryName(path);
            }

            _targetFolder = path?.Replace("\\", "/") ?? "Assets";
            Repaint();
        }

        private void OnGUI()
        {
            DrawHeader();
            EditorGUILayout.Space(10);

            using (new EditorGUILayout.HorizontalScope(GUILayout.ExpandHeight(true)))
            {
                DrawLeftTreeView();
                DrawRightTypeArea();
            }

            DrawBottomBar();
        }

        private void DrawHeader()
        {
            EditorGUI.BeginChangeCheck();
            _searchText = EditorGUILayout.TextField("Search", _searchText);
            if (EditorGUI.EndChangeCheck())
                OnSearchChanged();

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.TextField("Target Folder", _targetFolder);
                EditorGUI.EndDisabledGroup();

                if (GUILayout.Button("Update", GUILayout.Width(60)))
                    UpdateSelectedPath();
            }

            _fileName = EditorGUILayout.TextField("File Name", _fileName);

            if (_previewObject != null)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Type Configuration (Preview)", EditorStyles.boldLabel);

                if (_previewEditor == null)
                    _previewEditor = UnityEditor.Editor.CreateEditor(_previewObject);

                _previewEditor.OnInspectorGUI();
            }
        }

        private void DrawLeftTreeView()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(260)))
            {
                EditorGUILayout.LabelField(new GUIContent(" CATEGORIES", EditorGUIUtility.IconContent("Folder Icon").image), EditorStyles.boldLabel);

                using (var scroll = new EditorGUILayout.ScrollViewScope(_treeScroll))
                {
                    _treeScroll = scroll.scrollPosition;

                    if (string.IsNullOrWhiteSpace(_searchText))
                    {
                        foreach (var sub in _rootNode.SubCategories.Values.OrderBy(x => x.Name))
                            DrawNodeRecursive(sub, 0);
                    }
                    else
                    {
                        EditorGUILayout.HelpBox("Searching All Types...", MessageType.None);
                    }
                }
            }
        }

        private void DrawNodeRecursive(CategoryNode node, int depth)
        {
            bool hasChildren = node.SubCategories.Count > 0;
            bool isSelected = _selectedNode == node;

            Rect rect = EditorGUILayout.GetControlRect(true, 20);
            float indentSize = depth * 14f;

            if (isSelected)
            {
                GUI.Box(new Rect(rect.x - 2, rect.y, rect.width + 4, rect.height), "", (GUIStyle)"SelectionRect");
            }

            if (hasChildren)
            {
                Rect foldoutRect = new Rect(rect.x + indentSize, rect.y, 13, rect.height);
                node.IsExpanded = EditorGUI.Foldout(foldoutRect, node.IsExpanded, GUIContent.none, true);
            }

            Rect contentRect = new Rect(rect.x + indentSize + 13, rect.y, rect.width - indentSize - 13, rect.height);
            GUIContent labelContent = new GUIContent(node.Name, EditorGUIUtility.IconContent("Folder Icon").image);

            GUIStyle labelStyle = new GUIStyle(EditorStyles.label)
            {
                fontStyle = isSelected ? FontStyle.Bold : FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft
            };

            if (GUI.Button(contentRect, labelContent, labelStyle))
            {
                _selectedNode = node;
            }

            if (node.IsExpanded && hasChildren)
            {
                foreach (var sub in node.SubCategories.Values.OrderBy(x => x.Name))
                    DrawNodeRecursive(sub, depth + 1);
            }
        }

        private void DrawRightTypeArea()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(new GUIContent(" SELECT TYPE", EditorGUIUtility.IconContent("ScriptableObject Icon").image), EditorStyles.boldLabel);

                using (var scroll = new EditorGUILayout.ScrollViewScope(_listScroll))
                {
                    _listScroll = scroll.scrollPosition;

                    List<TypeData> rawList = !string.IsNullOrWhiteSpace(_searchText)
                        ? _searchResult
                        : (_selectedNode?.Types ?? new List<TypeData>());

                    var toDisplay = rawList.OrderBy(x => x.AssemblyName).ThenBy(x => x.DisplayName).ToList();

                    if (toDisplay.Count == 0)
                    {
                        GUILayout.Space(20);
                        EditorGUILayout.HelpBox("No ScriptableObject types in this category.", MessageType.None);
                    }

                    string lastAssembly = null;
                    foreach (var item in toDisplay)
                    {
                        if (item.AssemblyName != lastAssembly)
                        {
                            EditorGUILayout.LabelField(item.AssemblyName, EditorStyles.miniLabel);
                            lastAssembly = item.AssemblyName;
                        }

                        bool isSelected = _selectedTypeData == item;

                        Color assemblyColor = GetColorForAssembly(item.AssemblyName);
                        GUI.backgroundColor = isSelected
                            ? Color.Lerp(assemblyColor, Color.black, 0.6f)
                            : assemblyColor;

                        var btnContent = new GUIContent(
                            $" {item.DisplayName}  ({item.AssemblyName})",
                            EditorGUIUtility.IconContent("ScriptableObject Icon").image);

                        if (GUILayout.Button(btnContent, "Button", GUILayout.Height(30)))
                        {
                            SelectType(item);
                        }

                        GUI.backgroundColor = Color.white;
                    }
                }
            }
        }

        private static Color GetColorForAssembly(string assemblyName)
        {
            if (string.IsNullOrEmpty(assemblyName)) return Color.white;

            int hash = assemblyName.GetHashCode();
            float h = Mathf.Abs(hash % 360) / 360f;

            return Color.HSVToRGB(h, 0.35f, 0.9f);
        }

        private void DrawBottomBar()
        {
            EditorGUILayout.Space(5);

            using (new EditorGUILayout.VerticalScope())
            {
                var validId = IsInvalidPath ? 1 : IsWarningPath ? 2 : 0;
                GUI.backgroundColor = Color.black;
                GUI.contentColor = ValidationColors[validId];
                var icon = EditorGUIUtility.IconContent(ValidationIcons[validId]).image;
                GUILayout.Box(new GUIContent(ValidationMessages[validId], icon),
                    GUILayout.ExpandWidth(true),
                    GUILayout.Height(25));
                GUI.backgroundColor = Color.white;
                GUI.contentColor = Color.white;
            }

            using (new EditorGUI.DisabledScope(_selectedTypeData == null || IsInvalidPath))
            {
                GUI.backgroundColor = IsValidPath
                    ? (IsWarningPath ? Color.yellow : Color.green)
                    : Color.gray;

                var icon = EditorGUIUtility.IconContent("CreateAddNew").image;
                if (GUILayout.Button(new GUIContent("CREATE ASSET", icon), GUILayout.Height(40)))
                {
                    CreateAsset();
                }

                GUI.backgroundColor = Color.white;
            }
        }

        private void SelectType(TypeData data)
        {
            if (_selectedTypeData == data) return;
            _selectedTypeData = data;

            if (_previewObject != null) DestroyImmediate(_previewObject);
            if (_previewEditor != null)
            {
                DestroyImmediate(_previewEditor);
                _previewEditor = null;
            }

            _previewObject = CreateInstance(data.Type);
            _fileName = !string.IsNullOrEmpty(data.DefaultFileName) ? data.DefaultFileName : $"New_{data.Type.Name}";
        }

        private void CreateAsset()
        {
            if (_selectedTypeData == null || IsInvalidPath) return;

            if (!Directory.Exists(_targetFolder))
                Directory.CreateDirectory(_targetFolder);

            string fullPath = AssetDatabase.GenerateUniqueAssetPath(FileNamePreview);
            ScriptableObject finalAsset = Instantiate(_previewObject);
            AssetDatabase.CreateAsset(finalAsset, fullPath);
            AssetDatabase.SaveAssets();

            EditorUtility.FocusProjectWindow();
            Selection.activeObject = finalAsset;
            EditorGUIUtility.PingObject(finalAsset);
        }
    }
}
