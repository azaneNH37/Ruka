using Ruka.Core.DI;

namespace Ruka.Core.Resources
{
    public enum PlayMode
    {
        EditorSimulateMode,
        OfflinePlayMode
    }

    public sealed record ResourceConfig : IFeatureConfig
    {
        public string PackageName { get; init; } = "DefaultPackage";
        public PlayMode Mode { get; init; } = PlayMode.EditorSimulateMode;
    }
}
