using System.Collections.Generic;
using Ruka.Core.DI;
using UnityEngine;
using VContainer;

namespace Ruka.UI.Windows
{
    [FeatureInstaller(typeof(SceneGroup))]
    public sealed class WindowFeatureInstaller : IFeatureInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            builder.RegisterConfig(new WindowConfig());

            IReadOnlyList<SceneWindowRegistry> registries = Object.FindObjectsOfType<SceneWindowRegistry>();

            builder.Register<WindowManager>(Lifetime.Singleton)
                   .AsImplementedInterfaces()
                   .WithParameter(registries);
        }
    }
}
