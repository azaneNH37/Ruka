using Ruka.Core.DI;
using VContainer;

namespace Ruka.Core.Camera
{
    [FeatureInstaller(typeof(ProjectGroup), order: 20)]
    internal sealed class CameraInstaller : IFeatureInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            builder.Register<CameraRegistry>(Lifetime.Singleton)
                .As<ICameraProvider>()
                .As<ICameraRegistrar>();
        }
    }
}
