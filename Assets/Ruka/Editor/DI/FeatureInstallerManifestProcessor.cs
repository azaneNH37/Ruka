using System;
using System.Collections.Generic;
using Ruka.Core.DI;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace Ruka.Editor.DI
{
    public static class FeatureInstallerManifestProcessor
    {
        private static bool refreshQueued;
        private static bool refreshing;

        [DidReloadScripts]
        private static void OnScriptsReloaded()
        {
            RequestRefresh("Scripts reloaded");
        }

        internal static void RequestRefresh(string reason)
        {
            if (refreshQueued)
            {
                return;
            }

            refreshQueued = true;
            EditorApplication.delayCall += () =>
            {
                refreshQueued = false;
                RefreshCollectors(reason);
            };
        }

        private static void RefreshCollectors(string reason)
        {
            if (refreshing)
            {
                return;
            }

            refreshing = true;

            try
            {
                var installersByGroup = CollectFeatureInstallers();
                var collectors = FindCollectors();
                var hasChanges = false;

                for (var i = 0; i < collectors.Count; i++)
                {
                    var collector = collectors[i];
                    if (collector == null)
                    {
                        continue;
                    }

                    var group = collector.TargetGroup.Value;
                    if (string.IsNullOrWhiteSpace(group))
                    {
                        Debug.LogError($"[{nameof(FeatureInstallerManifestProcessor)}] {collector.name} has an empty target group.", collector);
                        continue;
                    }

                    installersByGroup.TryGetValue(group, out var types);
                    types ??= Array.Empty<string>();

                    if (!collector.UpdateQualifiedTypes(types))
                    {
                        continue;
                    }

                    EditorUtility.SetDirty(collector);
                    hasChanges = true;
                }

                if (hasChanges)
                {
                    AssetDatabase.SaveAssets();
                }

                LogScanResult(installersByGroup, collectors.Count, reason);
            }
            finally
            {
                refreshing = false;
            }
        }

        private static Dictionary<string, IReadOnlyList<string>> CollectFeatureInstallers()
        {
            var allTypes = TypeCache.GetTypesWithAttribute<FeatureInstallerAttribute>();
            var grouped = new Dictionary<string, List<(Type Type, int Order)>>(StringComparer.Ordinal);

            for (var i = 0; i < allTypes.Count; i++)
            {
                var type = allTypes[i];
                if (type == null || !type.IsClass || type.IsAbstract)
                {
                    continue;
                }

                if (!typeof(IFeatureInstaller).IsAssignableFrom(type))
                {
                    Debug.LogError($"[{nameof(FeatureInstallerManifestProcessor)}] {type.FullName} has {nameof(FeatureInstallerAttribute)} but does not implement {nameof(IFeatureInstaller)}.");
                    continue;
                }

                var attr = (FeatureInstallerAttribute)Attribute.GetCustomAttribute(type, typeof(FeatureInstallerAttribute));
                if (attr == null)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(attr.Group))
                {
                    Debug.LogError($"[{nameof(FeatureInstallerManifestProcessor)}] {type.FullName} has an empty Group in {nameof(FeatureInstallerAttribute)}.");
                    continue;
                }

                if (!grouped.TryGetValue(attr.Group, out var entries))
                {
                    entries = new List<(Type Type, int Order)>();
                    grouped[attr.Group] = entries;
                }

                entries.Add((type, attr.Order));
            }

            var result = new Dictionary<string, IReadOnlyList<string>>(grouped.Count, StringComparer.Ordinal);
            foreach (var pair in grouped)
            {
                pair.Value.Sort(static (left, right) =>
                {
                    var orderCompare = left.Order.CompareTo(right.Order);
                    if (orderCompare != 0)
                    {
                        return orderCompare;
                    }

                    return string.Compare(left.Type.FullName, right.Type.FullName, StringComparison.Ordinal);
                });

                var sorted = new string[pair.Value.Count];
                for (var i = 0; i < pair.Value.Count; i++)
                {
                    sorted[i] = pair.Value[i].Type.AssemblyQualifiedName;
                }

                result[pair.Key] = sorted;
            }

            return result;
        }

        private static List<FeatureGroupCollector> FindCollectors()
        {
            var guids = AssetDatabase.FindAssets("t:FeatureGroupCollector");
            var assets = new List<FeatureGroupCollector>(guids.Length);

            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var asset = AssetDatabase.LoadAssetAtPath<FeatureGroupCollector>(path);
                if (asset == null)
                {
                    Debug.LogError($"[{nameof(FeatureInstallerManifestProcessor)}] Failed to load FeatureGroupCollector at {path}.");
                    continue;
                }

                assets.Add(asset);
            }

            return assets;
        }

        private static void LogScanResult(Dictionary<string, IReadOnlyList<string>> installersByGroup, int assetCount, string reason)
        {
            if (installersByGroup.Count == 0)
            {
                Debug.Log($"<color=yellow>[{nameof(FeatureInstallerManifestProcessor)}] Scan completed ({reason}). No feature installers found. Assets: {assetCount}.</color>");
                return;
            }

            var lines = new List<string>(installersByGroup.Count + 1)
            {
                $"<color=green>[{nameof(FeatureInstallerManifestProcessor)}] Scan completed ({reason}). FeatureGroupCollector assets: {assetCount}.</color>"
            };

            var groupNames = new List<string>(installersByGroup.Keys);
            groupNames.Sort(StringComparer.Ordinal);

            for (var i = 0; i < groupNames.Count; i++)
            {
                var groupName = groupNames[i];
                var count = installersByGroup[groupName].Count;
                lines.Add($"- Group '{groupName}': {count} installer(s)");
            }

            Debug.Log(string.Join("\n", lines));
        }
    }
}
