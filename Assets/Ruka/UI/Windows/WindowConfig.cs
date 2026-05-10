using Ruka.Core.DI;
using UnityEngine;

namespace Ruka.UI.Windows
{
    public sealed record WindowConfig : FeatureConfig
    {
        public int LayerSpacing { get; init; } = 1000;
        public Vector2 ReferenceResolution { get; init; } = new(1920, 1080);
        public float MatchWidthOrHeight { get; init; } = 0.5f;
    }
}
