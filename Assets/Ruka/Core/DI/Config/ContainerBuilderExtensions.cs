using VContainer;

namespace Ruka.Core.DI
{
    public static class ContainerBuilderExtensions
    {
        /// <summary>
        /// Registers <paramref name="baseline"/> as a scoped <typeparamref name="T"/> instance,
        /// applying any matching <see cref="FeatureConfigOverride{T}"/> from the active scope before registration.
        /// Call inside <see cref="IFeatureInstaller.Install"/>; has no effect outside a scope build.
        /// </summary>
        public static void RegisterConfig<T>(this IContainerBuilder builder, T baseline)
            where T : IFeatureConfig
        {
            var applier = ConfigOverrideBuildContext.Current;
            var final = applier != null ? applier.Apply(baseline) : baseline;
            builder.RegisterInstance(final);
        }
    }
}
