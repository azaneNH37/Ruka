using Ruka.Core.DI;
using UnityEditor;

namespace Ruka.Editor.DI
{
    [CustomEditor(typeof(GroupedLifetimeScope), editorForChildClasses: true)]
    public sealed class GroupedLifetimeScopeEditor : UnityEditor.Editor
    {
        private bool installerDetailFoldout;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            DrawInstallerDetail();
        }

        private void DrawInstallerDetail()
        {
            var collectorsProp = serializedObject.FindProperty("collectors");
            if (collectorsProp == null || collectorsProp.arraySize == 0)
            {
                return;
            }

            EditorGUILayout.Space(4);

            installerDetailFoldout = EditorGUILayout.Foldout(installerDetailFoldout,
                "Installer Detail", true, EditorStyles.foldoutHeader);

            if (!installerDetailFoldout)
            {
                return;
            }

            EditorGUI.indentLevel++;

            for (var i = 0; i < collectorsProp.arraySize; i++)
            {
                var collector = collectorsProp.GetArrayElementAtIndex(i).objectReferenceValue as FeatureGroupCollector;
                if (collector == null || collector.QualifiedTypes.Count == 0)
                {
                    continue;
                }

                EditorGUILayout.LabelField(
                    collector.name,
                    $"group: {collector.TargetGroup.Value}  |  {collector.QualifiedTypes.Count} installer(s)",
                    EditorStyles.miniLabel);

                EditorGUI.indentLevel++;
                for (var j = 0; j < collector.QualifiedTypes.Count; j++)
                {
                    var st = collector.QualifiedTypes[j];
                    var typeName = st != null && st.IsValid ? st.Type.Name : "(unresolved)";
                    EditorGUILayout.LabelField($"[{j}]", typeName, EditorStyles.miniLabel);
                }

                EditorGUI.indentLevel--;
            }

            EditorGUI.indentLevel--;
        }
    }
}
