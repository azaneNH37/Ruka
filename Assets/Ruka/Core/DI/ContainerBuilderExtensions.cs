using UnityEngine.Rendering.VirtualTexturing;
using VContainer;

namespace Ruka.Core.DI
{
    public static class ContainerBuilderExtensions
    {
        public static void RegisterConfig<T>(this IContainerBuilder builder, T baseline)
            where T : notnull
        {
            builder.Register<T>(resolver => resolver.Resolve<ConfigOverrideApplier>().Apply<T>(baseline), Lifetime.Singleton);
        }
    }
}
