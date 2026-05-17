using Ruka.Core.DI;

namespace Ruka.Core.Clock
{
    public sealed record TickerConfig : IFeatureConfig
    {
        public int Frequency { get; init; } = 30;
    }
}
