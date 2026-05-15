using Ruka.Core.DI;

namespace Ruka.Core.Saves
{
    public sealed record CrossSaveConfig : FeatureConfig
    {
        public string FileName { get; init; } = "cross.sav";
        public int Version { get; init; } = 1;
        public bool AutoResaveOnMigrate { get; init; } = true;
    }
}
