using VContainer;

namespace Ruka.Core.DI
{
    public interface IFeatureInstaller
    {
        void Install(IContainerBuilder builder);
    }
}
