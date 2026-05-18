namespace Ruka.Core.DI
{
    /// <summary>
    /// Marker interface for plain-data config structs registered via <see cref="ContainerBuilderExtensions.RegisterConfig{T}"/>.
    /// Implement on value types only; do not include service references or runtime state.
    /// </summary>
    public interface IFeatureConfig { }
}
