using Ruka.Core.DI;
using Ruka.Core.Resources;
using Ruka.Core.Symbols;

namespace Ruka.Core.Scenes
{
    public record SceneTransitionConfig : FeatureConfig
    {
        public Symbol<AssetRef>? CurtainPrefabKey { get; init; } = null;
    }
}
