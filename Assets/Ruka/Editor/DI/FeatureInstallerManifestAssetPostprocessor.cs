using Ruka.Core.DI;
using UnityEditor;

namespace Ruka.Editor.DI
{
    public sealed class FeatureInstallerManifestAssetPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (importedAssets == null || importedAssets.Length == 0)
            {
                return;
            }

            for (var i = 0; i < importedAssets.Length; i++)
            {
                var path = importedAssets[i];
                if (!path.EndsWith(".asset"))
                {
                    continue;
                }

                var collectorAsset = AssetDatabase.LoadAssetAtPath<FeatureGroupCollector>(path);
                if (collectorAsset == null)
                {
                    continue;
                }

                FeatureInstallerManifestProcessor.RequestRefresh($"Asset imported: {path}");
                return;
            }
        }
    }
}
