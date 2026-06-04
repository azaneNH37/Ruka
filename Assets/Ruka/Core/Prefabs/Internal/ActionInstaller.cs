using System;
using System.Collections.Generic;
using VContainer;
using VContainer.Unity;

namespace Ruka.Core.Prefabs
{
    internal sealed class ActionInstaller : IInstaller
    {
        private readonly Action<IContainerBuilder> _installation;

        public ActionInstaller(Action<IContainerBuilder> installation)
        {
            _installation = installation;
        }

        public void Install(IContainerBuilder builder)
        {
            _installation(builder);
        }
    }

    internal sealed class CompositeInstaller : IInstaller
    {
        private readonly List<IInstaller> _installers;

        public CompositeInstaller(List<IInstaller> installers)
        {
            _installers = installers;
        }

        public void Install(IContainerBuilder builder)
        {
            foreach (var installer in _installers)
                installer.Install(builder);
        }
    }
}
