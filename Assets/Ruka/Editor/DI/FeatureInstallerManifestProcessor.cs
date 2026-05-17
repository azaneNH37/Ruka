using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Ruka.Core.DI;
using Ruka.Core.Symbols;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace Ruka.Editor.DI
{
    public static class FeatureInstallerManifestProcessor
    {
        private const string GeneratedDir = "Assets/Ruka.Generated";
        private const string CollectorsDir = "Assets/Ruka.Generated/Collectors";
        private const string ManifestPath = "Assets/Ruka.Generated/INSTALLER_MANIFEST.md";
        private const string LinkXmlPath = "Assets/Ruka.Generated/link.xml";

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
                RefreshAll(reason);
            };
        }

        private static void RefreshAll(string reason)
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

                var hasChanges = AutoCreateCollectors(installersByGroup, collectors);

                if (hasChanges)
                {
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                    collectors = FindCollectors();
                }

                hasChanges = false;

                for (var i = 0; i < collectors.Count; i++)
                {
                    var collector = collectors[i];
                    if (collector == null)
                    {
                        continue;
                    }

                    var group = collector.TargetGroup;
                    if (group == null)
                    {
                        Debug.LogError($"[{nameof(FeatureInstallerManifestProcessor)}] {collector.name} has no target group assigned.", collector);
                        continue;
                    }

                    installersByGroup.TryGetValue(group, out var entries);
                    var types = entries != null
                        ? entries.ConvertAll(e => e.Type)
                        : (IReadOnlyList<Type>)Array.Empty<Type>();

                    if (collector.UpdateQualifiedTypes(types))
                    {
                        EditorUtility.SetDirty(collector);
                        hasChanges = true;
                    }
                }

                if (hasChanges)
                {
                    AssetDatabase.SaveAssets();
                }

                GenerateInstallerManifest(installersByGroup);
                GenerateLinkXml(installersByGroup);
                ValidateRukaCoreProviderGroup();

                LogScanResult(installersByGroup, collectors.Count, reason);
            }
            finally
            {
                refreshing = false;
            }
        }

        private static Dictionary<Type, List<(Type Type, int Order)>> CollectFeatureInstallers()
        {
            var allTypes = TypeCache.GetTypesWithAttribute<FeatureInstallerAttribute>();
            var grouped = new Dictionary<Type, List<(Type Type, int Order)>>();

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

                FeatureInstallerAttribute attr;
                try
                {
                    attr = (FeatureInstallerAttribute)Attribute.GetCustomAttribute(type, typeof(FeatureInstallerAttribute));
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[{nameof(FeatureInstallerManifestProcessor)}] Invalid [FeatureInstaller] on {type.FullName}: {ex.Message}");
                    continue;
                }

                if (attr == null)
                {
                    continue;
                }

                var group = attr.Group;

                if (!grouped.TryGetValue(group, out var entries))
                {
                    entries = new List<(Type Type, int Order)>();
                    grouped[group] = entries;
                }

                entries.Add((type, attr.Order));
            }

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
            }

            return grouped;
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

        private static bool AutoCreateCollectors(
            Dictionary<Type, List<(Type Type, int Order)>> installersByGroup,
            List<FeatureGroupCollector> existingCollectors)
        {
            var collectorGroups = new HashSet<Type>();
            for (var i = 0; i < existingCollectors.Count; i++)
            {
                var c = existingCollectors[i];
                if (c?.TargetGroup != null)
                {
                    collectorGroups.Add(c.TargetGroup);
                }
            }

            var created = false;

            foreach (var group in installersByGroup.Keys)
            {
                if (collectorGroups.Contains(group))
                {
                    continue;
                }

                EnsureDirectoryExists(CollectorsDir);

                var collector = ScriptableObject.CreateInstance<FeatureGroupCollector>();
                SetCollectorTargetGroup(collector, group);

                var assetName = $"{SanitizeAssetName(group.Name)}_Collector.asset";
                var assetPath = Path.Combine(CollectorsDir, assetName).Replace("\\", "/");
                assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);

                AssetDatabase.CreateAsset(collector, assetPath);
                Debug.Log($"[{nameof(FeatureInstallerManifestProcessor)}] Auto-created collector for group '{group.FullName}' at {assetPath}.");
                created = true;
            }

            return created;
        }

        private static void SetCollectorTargetGroup(FeatureGroupCollector collector, Type groupType)
        {
            var so = new SerializedObject(collector);
            var targetGroupProp = so.FindProperty("targetGroup");
            if (targetGroupProp != null)
            {
                var aqnProp = targetGroupProp.FindPropertyRelative("assemblyQualifiedName");
                if (aqnProp != null)
                {
                    aqnProp.stringValue = groupType.AssemblyQualifiedName;
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static string SanitizeAssetName(string name)
        {
            var sb = new StringBuilder(name.Length);
            for (var i = 0; i < name.Length; i++)
            {
                var c = name[i];
                if (char.IsLetterOrDigit(c) || c == '_' || c == '-')
                {
                    sb.Append(c);
                }
                else
                {
                    sb.Append('_');
                }
            }

            return sb.ToString();
        }

        private static void GenerateInstallerManifest(
            Dictionary<Type, List<(Type Type, int Order)>> installersByGroup)
        {
            EnsureDirectoryExists(GeneratedDir);

            var sb = new StringBuilder();
            sb.AppendLine("# Ruka DI — Installer Index");
            sb.AppendLine();
            sb.AppendLine("> Auto-generated by `FeatureInstallerManifestProcessor` on `[DidReloadScripts]`.");
            sb.AppendLine("> Do not edit manually.");
            sb.AppendLine();
            sb.AppendLine("## How to use this file");
            sb.AppendLine();
            sb.AppendLine("This is a **discovery index**, not a registry manifest.");
            sb.AppendLine("Groups are discovered at compile-time via `TypeCache.GetTypesWithAttribute`.");
            sb.AppendLine("Installers are sorted by `order` then full type name.");
            sb.AppendLine();
            sb.AppendLine("1. Find the group you need in the [Summary](#summary) table.");
            sb.AppendLine("2. Read the installer source files listed under that group to see exact registrations.");
            sb.AppendLine("3. Registration details (`Register<T>`, lifetimes, `.As<>` bindings, `BuildCallback`,");
            sb.AppendLine("   conditional logic) live in the source files — this index is a navigation layer,");
            sb.AppendLine("   not a replacement for reading them.");
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();

            if (installersByGroup.Count == 0)
            {
                sb.AppendLine("_No feature installers found._");
            }
            else
            {
                var groupTypes = new List<Type>(installersByGroup.Keys);
                groupTypes.Sort((a, b) => string.Compare(a.FullName, b.FullName, StringComparison.Ordinal));

                var totalInstallers = 0;

                sb.AppendLine("## Summary");
                sb.AppendLine();
                sb.AppendLine("| Group | Installers |");
                sb.AppendLine("|-------|-----------|");

                foreach (var groupType in groupTypes)
                {
                    var count = installersByGroup[groupType].Count;
                    totalInstallers += count;
                    sb.AppendLine($"| {groupType.FullName} | {count} |");
                }

                sb.AppendLine($"| **Total** | **{totalInstallers}** |");
                sb.AppendLine();
                sb.AppendLine("---");
                sb.AppendLine();

                foreach (var groupType in groupTypes)
                {
                    var entries = installersByGroup[groupType];

                    sb.AppendLine($"## Group: {groupType.FullName}");
                    sb.AppendLine();

                    for (var j = 0; j < entries.Count; j++)
                    {
                        var type = entries[j].Type;
                        var order = entries[j].Order;
                        var assemblyName = type.Assembly.GetName().Name;
                        var fileName = $"{type.Name}.cs";

                        sb.Append($"- `{type.FullName}`");
                        sb.Append($"  [order: {order}");
                        sb.Append($", assembly: {assemblyName}");
                        sb.AppendLine($", file: {fileName}]");
                    }

                    sb.AppendLine();
                    sb.AppendLine("---");
                    sb.AppendLine();
                }
            }

            var existingContent = File.Exists(ManifestPath) ? File.ReadAllText(ManifestPath, Encoding.UTF8) : null;
            var newContent = sb.ToString();

            if (existingContent != newContent)
            {
                File.WriteAllText(ManifestPath, newContent, Encoding.UTF8);
                AssetDatabase.ImportAsset(ManifestPath);
                Debug.Log($"[{nameof(FeatureInstallerManifestProcessor)}] Updated {ManifestPath}");
            }
        }

        private static void GenerateLinkXml(
            Dictionary<Type, List<(Type Type, int Order)>> installersByGroup)
        {
            EnsureDirectoryExists(GeneratedDir);

            var assemblies = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

            foreach (var pair in installersByGroup)
            {
                for (var i = 0; i < pair.Value.Count; i++)
                {
                    var type = pair.Value[i].Type;
                    var assemblyName = type.Assembly.GetName().Name;

                    if (!assemblies.TryGetValue(assemblyName, out var types))
                    {
                        types = new HashSet<string>(StringComparer.Ordinal);
                        assemblies[assemblyName] = types;
                    }

                    types.Add(type.FullName);
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            sb.AppendLine("<!-- Auto-generated by FeatureInstallerManifestProcessor. Do not edit manually. -->");
            sb.AppendLine("<linker>");

            var assemblyNames = new List<string>(assemblies.Keys);
            assemblyNames.Sort(StringComparer.Ordinal);

            for (var i = 0; i < assemblyNames.Count; i++)
            {
                var assemblyName = assemblyNames[i];
                var types = assemblies[assemblyName];
                var typeNames = new List<string>(types);
                typeNames.Sort(StringComparer.Ordinal);

                sb.AppendLine($"  <assembly fullname=\"{assemblyName}\">");

                for (var j = 0; j < typeNames.Count; j++)
                {
                    sb.AppendLine($"    <type fullname=\"{typeNames[j]}\" preserve=\"all\" />");
                }

                sb.AppendLine("  </assembly>");
            }

            sb.AppendLine("</linker>");

            var newContent = sb.ToString();
            var existingContent = File.Exists(LinkXmlPath) ? File.ReadAllText(LinkXmlPath, Encoding.UTF8) : null;

            if (existingContent != newContent)
            {
                File.WriteAllText(LinkXmlPath, newContent, Encoding.UTF8);
                AssetDatabase.ImportAsset(LinkXmlPath);
                Debug.Log($"[{nameof(FeatureInstallerManifestProcessor)}] Updated {LinkXmlPath}");
            }
        }

        private static void ValidateRukaCoreProviderGroup()
        {
            var providerTypes = TypeCache.GetTypesWithAttribute<SymbolProviderAttribute>();

            for (var i = 0; i < providerTypes.Count; i++)
            {
                var type = providerTypes[i];
                if (type == null)
                {
                    continue;
                }

                var attrs = Attribute.GetCustomAttributes(type, typeof(SymbolProviderAttribute));
                foreach (var raw in attrs)
                {
                    var attr = raw as SymbolProviderAttribute;
                    if (attr == null)
                    {
                        continue;
                    }

                    if (!string.Equals(attr.GroupName, "RUKA_CORE", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var assemblyName = type.Assembly.GetName().Name;
                    if (assemblyName == null)
                    {
                        continue;
                    }

                    if (assemblyName.StartsWith("Ruka", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    Debug.LogError(
                        $"[{nameof(FeatureInstallerManifestProcessor)}] " +
                        $"Type '{type.FullName}' in assembly '{assemblyName}' uses " +
                        $"providerGroup \"RUKA_CORE\" which is reserved for framework assemblies. " +
                        $"Use a custom providerGroup string instead.");
                }
            }
        }

        private static void EnsureDirectoryExists(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureDirectoryExists(parent);
            }

            var folderName = Path.GetFileName(path);
            AssetDatabase.CreateFolder(parent ?? "Assets", folderName);
        }

        private static void LogScanResult(
            Dictionary<Type, List<(Type Type, int Order)>> installersByGroup,
            int assetCount,
            string reason)
        {
            if (installersByGroup.Count == 0)
            {
                Debug.Log($"<color=yellow>[{nameof(FeatureInstallerManifestProcessor)}] Scan completed ({reason}). No feature installers found. Collectors: {assetCount}.</color>");
                return;
            }

            var lines = new List<string>(installersByGroup.Count + 1)
            {
                $"<color=green>[{nameof(FeatureInstallerManifestProcessor)}] Scan completed ({reason}). Collectors: {assetCount}.</color>"
            };

            var groupTypes = new List<Type>(installersByGroup.Keys);
            groupTypes.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));

            for (var i = 0; i < groupTypes.Count; i++)
            {
                var groupType = groupTypes[i];
                var count = installersByGroup[groupType].Count;
                lines.Add($"- Group '{groupType.Name}': {count} installer(s)");
            }

            Debug.Log(string.Join("\n", lines));
        }
    }
}
