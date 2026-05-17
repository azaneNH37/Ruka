using Ruka.Core.DI;

namespace Ruka.Core.Saves
{
    public sealed record SavesConfig : IFeatureConfig
    {
        public string SaveFolder { get; init; } = "Saves";
        public string SaveFilePrefix { get; init; } = "save_";
        public int MaxSaveSlots { get; init; } = 5;
        public int Version { get; init; } = 1;
        public bool AutoResaveOnMigrate { get; init; } = true;
    }
}
