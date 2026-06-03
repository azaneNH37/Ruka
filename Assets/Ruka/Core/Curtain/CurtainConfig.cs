using Ruka.Core.DI;
using Ruka.Core.Resources;
using Ruka.Core.Symbols;

namespace Ruka.Core.Curtain
{
    public record CurtainConfig : IFeatureConfig
    {
        public Symbol<AssetRef>? CurtainPrefabKey { get; init; } = null;
    }
}
