using VContainer;

namespace Ruka.Core.DI
{
    public static class ContainerBuilderExtensions
    {
        public static void RegisterConfig<T>(this IContainerBuilder builder, T baseline)
            where T : IFeatureConfig
        {
            var applier = ConfigOverrideBuildContext.Current;
            var final = applier != null ? applier.Apply(baseline) : baseline;
            builder.RegisterInstance(final);
        }
    }
}
