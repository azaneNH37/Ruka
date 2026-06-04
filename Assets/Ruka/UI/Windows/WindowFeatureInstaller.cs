using Ruka.Core.DI;
using VContainer;

namespace Ruka.UI.Windows
{
    [FeatureInstaller(typeof(ProjectGroup))]
    public sealed class WindowFeatureInstaller : IFeatureInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            builder.RegisterConfig(new WindowConfig());

            builder.Register<WindowManager>(Lifetime.Singleton)
                   .AsSelf()
                   .AsImplementedInterfaces();

            builder.Register<WindowService>(Lifetime.Scoped)
                   .As<IWindowService>();
        }
    }
}
