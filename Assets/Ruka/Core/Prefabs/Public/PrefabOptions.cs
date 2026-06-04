using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Ruka.Core.Prefabs
{
    public class PrefabOptions
    {
        internal Transform VisualParent;
        internal LifetimeScope DiParentOverride;
        internal List<IInstaller> Installers;
        internal bool IsManualActivation;
        internal bool IsWorldPositionStays;
        internal Vector3? Position;
        internal Quaternion? Rotation;

        public PrefabOptions Under(Transform parent)
        {
            VisualParent = parent;
            return this;
        }

        public PrefabOptions WithDiParent(LifetimeScope scope)
        {
            DiParentOverride = scope;
            return this;
        }

        public PrefabOptions WithInstallation(Action<IContainerBuilder> installation)
        {
            Installers ??= new List<IInstaller>();
            Installers.Add(new ActionInstaller(installation));
            return this;
        }

        public PrefabOptions WithInstallation(IInstaller installer)
        {
            Installers ??= new List<IInstaller>();
            Installers.Add(installer);
            return this;
        }

        public PrefabOptions ManualActivation()
        {
            IsManualActivation = true;
            return this;
        }

        public PrefabOptions WorldPositionStays()
        {
            IsWorldPositionStays = true;
            return this;
        }

        public PrefabOptions At(Vector3 position)
        {
            Position = position;
            return this;
        }

        public PrefabOptions At(Vector3 position, Quaternion rotation)
        {
            Position = position;
            Rotation = rotation;
            return this;
        }
    }
}
