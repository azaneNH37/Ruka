using Ruka.Core.DI;
using Ruka.Core.Resources;
using Ruka.Core.Symbols;
using UnityEngine;

namespace Ruka.UI.Windows
{
    public sealed record WindowConfig : IFeatureConfig
    {
        public int LayerSpacing { get; init; } = 1000;
        public Vector2 ReferenceResolution { get; init; } = new(1920, 1080);
        public float MatchWidthOrHeight { get; init; } = 0.5f;

        /// <summary>
        /// Canvas prefab to instantiate as DDOL window root.
        /// When set, takes precedence over <see cref="UISceneKey"/>.
        /// When both are null, a default DDOL ScreenSpaceOverlay Canvas is created.
        /// </summary>
        public Symbol<AssetRef>? CanvasPrefabKey { get; init; } = null;

        /// <summary>
        /// Scene to load additively as a persistent UI scene.
        /// The root Canvas in the loaded scene is used as the window root.
        /// Only evaluated when <see cref="CanvasPrefabKey"/> is null.
        /// </summary>
        public Symbol<AssetRef>? UISceneKey { get; init; } = null;
    }
}
