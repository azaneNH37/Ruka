using MessagePipe;
using Ruka.Core.DI;
using VContainer;

namespace Ruka.Core.MessageBus
{
    [FeatureInstaller(InstallerGroups.ProjectGroup, order: 10)]
    public sealed class MessagePipeProjectInstaller : IFeatureInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            builder.RegisterMessagePipe();
            builder.RegisterBuildCallback(c => GlobalMessagePipe.SetProvider(c.AsServiceProvider()));
        }
    }
}
