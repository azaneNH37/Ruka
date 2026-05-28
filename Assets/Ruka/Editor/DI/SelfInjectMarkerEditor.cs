using System.Collections.Generic;
using System.Reflection;
using Ruka.Core.DI;
using UnityEditor;
using UnityEngine;
using VContainer.Unity;

namespace Ruka.Editor.DI
{
    [CustomEditor(typeof(SelfInjectMarker))]
    public sealed class SelfInjectMarkerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var go = ((SelfInjectMarker)target).gameObject;
            var (scope, isSameGo, isValid) = ResolveScope(go);

            EditorGUILayout.Space(2);
            DrawStatusSection(scope, isSameGo, isValid);
            EditorGUILayout.Space(6);
            DrawInjectableSection(go, isSameGo, isValid);
        }

        // ── Status ────────────────────────────────────────────────

        private static void DrawStatusSection(LifetimeScope scope, bool isSameGo, bool isValid)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                DrawRow("Scope", scope != null ? scope.name : "(none)");
                DrawRow("Range", GetRangeLabel(isValid, isSameGo));
            }

            if (!isValid)
            {
                EditorGUILayout.Space(2);
                var msg = isSameGo && scope != null
                    ? "LifetimeScope on this GO is not a NestedLifetimeScope — marker has no effect."
                    : "No enclosing NestedLifetimeScope found — marker has no effect.";
                EditorGUILayout.HelpBox(msg, MessageType.Warning);
            }
        }

        private static string GetRangeLabel(bool isValid, bool isSameGo)
        {
            if (!isValid) return "—";
            return isSameGo ? "This GO only  (non-recursive)" : "This GO + children";
        }

        private static void DrawRow(string key, string value)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(key, GUILayout.Width(48));
                EditorGUILayout.LabelField(value, EditorStyles.boldLabel);
            }
        }

        // ── Injectable list ───────────────────────────────────────

        private static void DrawInjectableSection(GameObject go, bool isSameGo, bool isValid)
        {
            EditorGUILayout.LabelField("Injected Components", EditorStyles.boldLabel);
            EditorGUILayout.Space(2);

            if (!isValid)
            {
                DrawEmptyList("—");
                return;
            }

            var items = CollectInjectables(go, isSameGo);

            if (items.Count == 0)
            {
                DrawEmptyList("No [Inject] members found in range");
                return;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                foreach (var (script, goName) in items)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField("•", GUILayout.Width(14));
                        EditorGUILayout.LabelField(script, GUILayout.MinWidth(80), GUILayout.ExpandWidth(false));
                        EditorGUILayout.LabelField($"({goName})", EditorStyles.miniLabel);
                    }
                }
            }
        }

        private static void DrawEmptyList(string text)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var style = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter };
                EditorGUILayout.LabelField(text, style);
            }
        }

        // ── Data ─────────────────────────────────────────────────

        private static (LifetimeScope scope, bool isSameGo, bool isValid) ResolveScope(GameObject go)
        {
            var sameGoScope = go.GetComponent<LifetimeScope>();
            if (sameGoScope != null)
                return (sameGoScope, true, sameGoScope is NestedLifetimeScope);

            var owning = FindOwningScope(go.transform);
            return (owning, false, owning != null);
        }

        private static NestedLifetimeScope FindOwningScope(Transform from)
        {
            var current = from.parent;
            while (current != null)
            {
                if (current.TryGetComponent<LifetimeScope>(out var scope))
                    return scope as NestedLifetimeScope;
                current = current.parent;
            }
            return null;
        }

        private static List<(string script, string goName)> CollectInjectables(GameObject root, bool selfOnly)
        {
            var results = new List<(string, string)>();
            CollectFromGO(root, results);
            if (!selfOnly)
                CollectFromChildrenRecursive(root.transform, results);
            return results;
        }

        private static void CollectFromChildrenRecursive(Transform t, List<(string, string)> results)
        {
            for (var i = 0; i < t.childCount; i++)
            {
                var child = t.GetChild(i);
                if (child.TryGetComponent<LifetimeScope>(out _)) continue;
                CollectFromGO(child.gameObject, results);
                CollectFromChildrenRecursive(child, results);
            }
        }

        private static void CollectFromGO(GameObject go, List<(string, string)> results)
        {
            foreach (var mb in go.GetComponents<MonoBehaviour>())
            {
                if (mb == null || mb is SelfInjectMarker) continue;
                if (HasAnyInjectMember(mb.GetType()))
                    results.Add((mb.GetType().Name, go.name));
            }
        }

        private static bool HasAnyInjectMember(System.Type type)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (var member in type.GetMembers(flags))
                if (HasInjectAttribute(member)) return true;
            return false;
        }

        private static bool HasInjectAttribute(MemberInfo member)
        {
            foreach (var attr in member.GetCustomAttributes(inherit: true))
            {
                var t = attr.GetType();
                if (t.Name == "InjectAttribute" && t.Namespace == "VContainer")
                    return true;
            }
            return false;
        }
    }
}
